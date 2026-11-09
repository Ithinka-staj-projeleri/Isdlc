import { Component, computed, inject, input, signal } from '@angular/core';
import { rxResource, takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { debounceTime, distinctUntilChanged, Subject } from 'rxjs';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { PaginationComponent } from '../../../shared/components/pagination/pagination.component';
import { StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';
import {
  REQUIREMENT_PRIORITY_LABELS, REQUIREMENT_PRIORITY_TONES, REQUIREMENT_STATUS_LABELS, REQUIREMENT_STATUS_TONES,
  REQUIREMENT_TYPE_LABELS, RequirementListItem, RequirementPriority, RequirementStatus, RequirementType,
} from '../models/requirement';
import { RequirementFormComponent } from '../requirement-form/requirement-form.component';
import { RequirementService } from '../requirement.service';

/**
 * The project's requirements: filters, search, paging, add / edit and status.
 * Takes the project id, so the menu page and the project detail page can both show it.
 */
@Component({
  selector: 'app-requirement-list',
  imports: [FormsModule, EmptyStateComponent, PaginationComponent, StatusBadgeComponent, RequirementFormComponent],
  templateUrl: './requirement-list.component.html',
  styleUrl: './requirement-list.component.scss',
})
export class RequirementListComponent {
  private readonly _requirementService = inject(RequirementService);

  readonly projectId = input.required<number>();

  /** True when the project is not active: records can be read but not added or changed. */
  readonly readOnly = input(false);

  protected readonly typeLabels = REQUIREMENT_TYPE_LABELS;
  protected readonly priorityLabels = REQUIREMENT_PRIORITY_LABELS;
  protected readonly statusLabels = REQUIREMENT_STATUS_LABELS;
  protected readonly priorityTones = REQUIREMENT_PRIORITY_TONES;
  protected readonly statusTones = REQUIREMENT_STATUS_TONES;
  protected readonly types = Object.keys(REQUIREMENT_TYPE_LABELS) as RequirementType[];
  protected readonly priorities = Object.keys(REQUIREMENT_PRIORITY_LABELS) as RequirementPriority[];
  protected readonly statuses = Object.keys(REQUIREMENT_STATUS_LABELS) as RequirementStatus[];

  protected readonly pageSize = 20;
  protected readonly page = signal(1);
  protected readonly type = signal<RequirementType | ''>('');
  protected readonly priority = signal<RequirementPriority | ''>('');
  protected readonly status = signal<RequirementStatus | ''>('');
  protected readonly search = signal('');

  /** null: form closed; 'new': adding; a number: editing that record. */
  protected readonly editing = signal<'new' | number | null>(null);

  /** The row whose status is being saved, so its select is disabled meanwhile. */
  protected readonly busyId = signal<number | null>(null);

  protected readonly requirements = rxResource({
    params: () => ({
      projectId: this.projectId(),
      page: this.page(),
      filter: {
        type: this.type() || undefined,
        priority: this.priority() || undefined,
        status: this.status() || undefined,
        search: this.search() || undefined,
      },
    }),
    stream: ({ params }) =>
      this._requirementService.list(params.projectId, params.filter, params.page, this.pageSize),
  });

  protected readonly hasFilter = computed(() => !!(this.type() || this.priority() || this.status() || this.search()));

  /** Search runs 300 ms after typing stops, not on every key. */
  private readonly _searchInput = new Subject<string>();

  constructor() {
    this._searchInput
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed())
      .subscribe((text) => this.applyFilter(() => this.search.set(text.trim())));
  }

  protected onSearchInput(text: string): void {
    this._searchInput.next(text);
  }

  protected setType(value: RequirementType | ''): void {
    this.applyFilter(() => this.type.set(value));
  }

  protected setPriority(value: RequirementPriority | ''): void {
    this.applyFilter(() => this.priority.set(value));
  }

  protected setStatus(value: RequirementStatus | ''): void {
    this.applyFilter(() => this.status.set(value));
  }

  protected changeStatus(requirement: RequirementListItem, status: RequirementStatus): void {
    if (status === requirement.status) {
      return;
    }

    this.busyId.set(requirement.id);
    this._requirementService.updateStatus(this.projectId(), requirement.id, status).subscribe({
      next: () => {
        this.busyId.set(null);
        this.requirements.reload();
      },
      // The error interceptor tells the user; reloading puts the select back to the saved status.
      error: () => {
        this.busyId.set(null);
        this.requirements.reload();
      },
    });
  }

  protected onSaved(): void {
    this.editing.set(null);
    this.requirements.reload();
  }

  /** A new filter shows its results from the first page. */
  private applyFilter(change: () => void): void {
    change();
    this.page.set(1);
  }
}
