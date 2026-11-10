import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject } from '@angular/core';
import { rxResource, toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { map } from 'rxjs';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';
import { DateOnlyPipe } from '../../../shared/pipes/date-only.pipe';
import { LocalDatePipe } from '../../../shared/pipes/local-date-pipe';
import { AuditLogComponent } from '../../audit/audit-log/audit-log.component';
import { DocumentListComponent } from '../../documents/document-list/document-list.component';
import { MeetingListComponent } from '../../meetings/meeting-list/meeting-list.component';
import { OpenIssueListComponent } from '../../open-issues/open-issue-list/open-issue-list.component';
import { ProjectProcessComponent } from '../../process/project-process/project-process.component';
import { RequirementListComponent } from '../../requirements/requirement-list/requirement-list.component';
import {
  PROJECT_PRIORITY_LABELS,
  PROJECT_STATUS_LABELS,
  PROJECT_STATUS_TONES,
  REQUEST_CHANNEL_LABELS,
  REQUEST_SOURCE_LABELS,
  REQUEST_TYPE_LABELS,
} from '../project-labels';
import { ProjectService } from '../project.service';

type DetailTab = 'general' | 'process' | 'requirements' | 'documents' | 'open-issues' | 'meetings' | 'history';

/** Stages 1–3 are the request phase ("Talepler"); 4–13 the project phase ("Projeler"). */
const LAST_REQUEST_STAGE = 3;

/**
 * Project detail: a summary on top and one tab per module.
 * Each tab is its owner's component and only needs the project id (docs/projects-api.md → Ekranlar).
 * The chosen tab is kept in the URL (?tab=process).
 */
@Component({
  selector: 'app-project-detail',
  imports: [
    RouterLink,
    DateOnlyPipe,
    LocalDatePipe,
    EmptyStateComponent,
    StatusBadgeComponent,
    ProjectProcessComponent,
    OpenIssueListComponent,
    MeetingListComponent,
    RequirementListComponent,
    DocumentListComponent,
    AuditLogComponent,
  ],
  templateUrl: './project-detail.component.html',
  styleUrl: './project-detail.component.scss',
})
export class ProjectDetailComponent {
  private readonly _route = inject(ActivatedRoute);
  private readonly _router = inject(Router);
  private readonly _projectService = inject(ProjectService);

  readonly statusLabels = PROJECT_STATUS_LABELS;
  readonly statusTones = PROJECT_STATUS_TONES;
  readonly priorityLabels = PROJECT_PRIORITY_LABELS;
  readonly typeLabels = REQUEST_TYPE_LABELS;
  readonly sourceLabels = REQUEST_SOURCE_LABELS;
  readonly channelLabels = REQUEST_CHANNEL_LABELS;

  readonly tabs: { key: DetailTab; label: string }[] = [
    { key: 'general', label: 'Genel Bilgiler' },
    { key: 'process', label: 'Süreç' },
    { key: 'requirements', label: 'Gereksinimler' },
    { key: 'documents', label: 'Dokümanlar' },
    { key: 'open-issues', label: 'Açık Konular' },
    { key: 'meetings', label: 'Toplantı Tutanakları' },
    { key: 'history', label: 'Kayıt Geçmişi' },
  ];

  readonly projectId = toSignal(this._route.paramMap.pipe(map((params) => Number(params.get('projectId')))), {
    requireSync: true,
  });

  readonly tab = toSignal(
    this._route.queryParamMap.pipe(map((params) => (params.get('tab') as DetailTab | null) ?? 'general')),
    { requireSync: true },
  );

  readonly project = rxResource({
    params: () => ({ projectId: this.projectId() }),
    stream: ({ params }) => this._projectService.get(params.projectId),
  });

  /** The project when loaded. Reading a resource's value in the error state throws, so it is checked first. */
  readonly loaded = computed(() => (this.project.hasValue() ? this.project.value() : undefined));

  readonly notFound = computed(() => {
    const error = this.project.error();
    const cause = (error as { cause?: unknown } | undefined)?.cause ?? error;
    return cause instanceof HttpErrorResponse && cause.status === 404;
  });

  /** Back to the list the project belongs to. */
  readonly backLink = computed(() => {
    const stageOrder = this.loaded()?.currentStep?.stageOrder;
    return stageOrder !== undefined && stageOrder <= LAST_REQUEST_STAGE
      ? { path: '/requests', label: 'Talepler' }
      : { path: '/projects', label: 'Projeler' };
  });

  /** Open issues and meetings can only be changed while the project is active. */
  readonly readOnly = computed(() => this.loaded()?.status !== 'Active');

  selectTab(tab: DetailTab): void {
    this._router.navigate([], {
      relativeTo: this._route,
      queryParams: { tab: tab === 'general' ? null : tab },
      queryParamsHandling: 'merge',
    });
  }
}
