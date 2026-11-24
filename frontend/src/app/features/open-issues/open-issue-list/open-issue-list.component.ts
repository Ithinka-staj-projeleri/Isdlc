import { Component, computed, effect, inject, input, output, signal, untracked } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { PaginationComponent } from '../../../shared/components/pagination/pagination.component';
import { BadgeTone, StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';
import { DateOnlyPipe } from '../../../shared/pipes/date-only.pipe';
import {
    OPEN_ISSUE_STATUS_LABELS, OPEN_ISSUE_TYPE_LABELS, OpenIssueListItem, OpenIssueStatus, OpenIssueType,
} from '../models/open-issue';
import { OpenIssueFormComponent } from '../open-issue-form/open-issue-form.component';
import { OpenIssueService } from '../open-issue.service';

const TYPE_TONES: Record<OpenIssueType, BadgeTone> = {
    Question: 'info',
    Action: 'info',
    Decision: 'ok',
    ChangeRequest: 'warn',
    Dependency: 'neutral',
    Assumption: 'neutral',
};

const STATUS_TONES: Record<OpenIssueStatus, BadgeTone> = {
    Open: 'info',
    InProgress: 'warn',
    Closed: 'neutral',
};

/**
 * The project's Open Issues List (flow 6.3): filters, paging, add / edit, close / reopen.
 * Takes the project id, so the menu page and the project detail page can both show it.
 */
@Component({
    selector: 'app-open-issue-list',
    imports: [FormsModule, EmptyStateComponent, PaginationComponent, StatusBadgeComponent, DateOnlyPipe, OpenIssueFormComponent],
    templateUrl: './open-issue-list.component.html',
    styleUrl: './open-issue-list.component.scss',
})
export class OpenIssueListComponent {
    private readonly openIssueService = inject(OpenIssueService);

    readonly projectId = input.required<number>();

    /** True when the project is not active: records can be read but not added or changed. */
    readonly readOnly = input(false);

    /** The record to open: ?id= in the address (docs/record-links.md). */
    readonly openId = input<number | null>(null);

    /** The opened record was closed, so the page can remove ?id= from the address. */
    readonly recordClosed = output<void>();

    constructor() {
        effect(() => {
            const id = this.openId();
            if (id !== null) untracked(() => this.editing.set(id));
        });
    }

    protected readonly typeLabels = OPEN_ISSUE_TYPE_LABELS;
    protected readonly statusLabels = OPEN_ISSUE_STATUS_LABELS;
    protected readonly typeTones = TYPE_TONES;
    protected readonly statusTones = STATUS_TONES;
    protected readonly types = Object.keys(OPEN_ISSUE_TYPE_LABELS) as OpenIssueType[];
    protected readonly statuses = Object.keys(OPEN_ISSUE_STATUS_LABELS) as OpenIssueStatus[];

    protected readonly pageSize = 20;
    protected readonly page = signal(1);
    protected readonly type = signal<OpenIssueType | ''>('');
    protected readonly status = signal<OpenIssueStatus | ''>('');
    protected readonly overdueOnly = signal(false);

    /** null: form closed; 'new': adding; a number: editing that record. */
    protected readonly editing = signal<'new' | number | null>(null);

    /** The row whose status is being saved, so its buttons are disabled meanwhile. */
    protected readonly busyId = signal<number | null>(null);

    protected readonly issues = rxResource({
        params: () => ({
            projectId: this.projectId(),
            page: this.page(),
            filter: { type: this.type() || undefined, status: this.status() || undefined, overdueOnly: this.overdueOnly() },
        }),
        stream: ({ params }) => this.openIssueService.list(params.projectId, params.filter, params.page, this.pageSize),
    });

    protected readonly hasFilter = computed(() => !!this.type() || !!this.status() || this.overdueOnly());

    /** A new filter shows its results from the first page. */
    protected setType(value: OpenIssueType | ''): void {
        this.type.set(value);
        this.page.set(1);
    }

    protected setStatus(value: OpenIssueStatus | ''): void {
        this.status.set(value);
        this.page.set(1);
    }

    protected setOverdueOnly(value: boolean): void {
        this.overdueOnly.set(value);
        this.page.set(1);
    }

    protected changeStatus(issue: OpenIssueListItem, status: OpenIssueStatus): void {
        this.busyId.set(issue.id);
        this.openIssueService.updateStatus(this.projectId(), issue.id, status).subscribe({
            next: () => {
                this.busyId.set(null);
                this.issues.reload();
            },
            error: () => this.busyId.set(null),
        });
    }

    protected onSaved(): void {
        this.closeForm();
        this.issues.reload();
    }

    protected closeForm(): void {
        this.editing.set(null);
        this.recordClosed.emit();
    }
}
