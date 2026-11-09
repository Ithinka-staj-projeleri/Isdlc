import { DatePipe } from '@angular/common';
import { Component, computed, inject, linkedSignal } from '@angular/core';
import { rxResource, takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Params, Router, RouterLink } from '@angular/router';
import { debounceTime, distinctUntilChanged, map, Subject } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { PaginationComponent } from '../../../shared/components/pagination/pagination.component';
import { StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';
import { PROJECT_STATUS_LABELS, PROJECT_STATUS_TONES, REQUEST_TYPE_LABELS } from '../project-labels';
import { PagedResult } from '../../../shared/models/paged-result';
import { ProjectFilter, ProjectListItem, ProjectStatus } from '../project.model';
import { ProjectService } from '../project.service';

/** Which menu opened the list: "Talepler" (stages 1–3) or "Projeler" (stages 4–13 and closed ones). */
export type ProjectListMode = 'requests' | 'projects';

/** The "Durum" choice on the Projeler page: active projects, everything, or one closed status. */
type ProjectView = 'active' | 'all' | Exclude<ProjectStatus, 'Active'>;

const PAGE_SIZE = 20;

/**
 * The project list. Filters live in the URL (?view=Closed&search=portal&page=2),
 * so a refresh, a shared link or the back button shows the same list.
 */
@Component({
  selector: 'app-project-list',
  imports: [DatePipe, RouterLink, EmptyStateComponent, PaginationComponent, StatusBadgeComponent],
  templateUrl: './project-list.component.html',
  styleUrl: './project-list.component.scss',
})
export class ProjectListComponent {
  private readonly _route = inject(ActivatedRoute);
  private readonly _router = inject(Router);
  private readonly _projectService = inject(ProjectService);
  private readonly _auth = inject(AuthService);

  readonly statusLabels = PROJECT_STATUS_LABELS;
  readonly statusTones = PROJECT_STATUS_TONES;
  readonly requestTypeLabels = REQUEST_TYPE_LABELS;
  readonly pageSize = PAGE_SIZE;

  readonly viewOptions: { value: ProjectView; label: string }[] = [
    { value: 'active', label: 'Aktif projeler' },
    { value: 'all', label: 'Tüm kayıtlar' },
    { value: 'Closed', label: 'Kapandı' },
    { value: 'Rejected', label: 'Reddedildi' },
    { value: 'Lost', label: 'Kaybedildi' },
    { value: 'Redirected', label: 'Yönlendirildi' },
  ];

  readonly mode = toSignal(this._route.data.pipe(map((data) => data['mode'] as ProjectListMode)), {
    initialValue: 'projects' as ProjectListMode,
  });
  private readonly _query = toSignal(this._route.queryParamMap, { requireSync: true });

  /** Only a business analyst can open a request (the API answers 403 to others). */
  readonly canOpenRequest = computed(() => this._auth.hasRole('BusinessAnalyst'));

  readonly title = computed(() => (this.mode() === 'requests' ? 'Talepler' : 'Projeler'));
  readonly view = computed(() => (this._query().get('view') as ProjectView | null) ?? 'active');
  readonly search = computed(() => this._query().get('search') ?? '');
  readonly analystId = computed(() => this._query().get('analyst') ?? '');
  readonly page = computed(() => Number(this._query().get('page')) || 1);

  /** URL → API filter. */
  readonly filter = computed<ProjectFilter>(() => {
    const filter: ProjectFilter = {
      search: this.search() || undefined,
      businessAnalystId: this.analystId() ? Number(this.analystId()) : undefined,
      page: this.page(),
      pageSize: PAGE_SIZE,
    };

    if (this.mode() === 'requests') {
      return { ...filter, phase: 'Request' };
    }

    // The Projeler page never shows waiting requests: they are on the Talepler page.
    const view = this.view();
    if (view === 'active') {
      return { ...filter, phase: 'Project', status: 'Active' };
    }
    return view === 'all' ? { ...filter, phase: 'Project' } : { ...filter, phase: 'Project', status: view };
  });

  /** Reloads whenever the filter changes; a newer request cancels the older one. */
  readonly projects = rxResource({
    params: () => this.filter(),
    stream: ({ params }) => this._projectService.list(params),
  });

  private readonly _analysts = rxResource({
    stream: () => this._projectService.users('BusinessAnalyst'),
  });

  // A resource throws when its value is read in the error state (e.g. the API is down),
  // which would stop the whole screen from updating. Read it only when there is a value.
  readonly analystOptions = computed(() => (this._analysts.hasValue() ? this._analysts.value() : []));

  /** The last loaded page. Kept while the next one loads, so the table fades instead of disappearing. */
  private readonly _lastResult = linkedSignal<PagedResult<ProjectListItem> | undefined, PagedResult<ProjectListItem> | undefined>({
    source: () => (this.projects.hasValue() ? this.projects.value() : undefined),
    computation: (current, previous) => current ?? previous?.value,
  });

  readonly rows = computed(() => this._lastResult()?.items ?? []);
  readonly totalCount = computed(() => this._lastResult()?.totalCount ?? 0);

  /** Search runs 300 ms after typing stops, not on every key. */
  private readonly _searchInput = new Subject<string>();

  constructor() {
    this._searchInput
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed())
      .subscribe((text) => this.setFilter({ search: text.trim() || null }));
  }

  onSearchInput(text: string): void {
    this._searchInput.next(text);
  }

  onViewChange(view: string): void {
    this.setFilter({ view: view === 'active' ? null : view });
  }

  onAnalystChange(analystId: string): void {
    this.setFilter({ analyst: analystId || null });
  }

  openProject(projectId: number): void {
    this._router.navigate(['/projects', projectId]);
  }

  onPageChange(page: number): void {
    this._router.navigate([], {
      relativeTo: this._route,
      queryParams: { page: page === 1 ? null : page },
      queryParamsHandling: 'merge',
    });
  }

  /** Changing a filter starts again from page 1. Null removes the parameter from the URL. */
  private setFilter(changes: Params): void {
    this._router.navigate([], {
      relativeTo: this._route,
      queryParams: { ...changes, page: null },
      queryParamsHandling: 'merge',
    });
  }
}
