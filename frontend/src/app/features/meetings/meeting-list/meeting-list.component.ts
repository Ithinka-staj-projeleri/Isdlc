import { Component, computed, effect, inject, input, output, signal, untracked } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { PaginationComponent } from '../../../shared/components/pagination/pagination.component';
import { DateOnlyPipe } from '../../../shared/pipes/date-only.pipe';
import { MeetingDetailComponent } from '../meeting-detail/meeting-detail.component';
import { MeetingFormComponent } from '../meeting-form/meeting-form.component';
import { MeetingService } from '../meeting.service';
import { MEETING_TYPE_LABELS } from '../models/meeting';

/** What is open over the list. */
type Panel = { kind: 'detail'; meetingId: number } | { kind: 'edit'; meetingId: number } | { kind: 'new' };

/**
 * The project's meeting minutes, newest first. Takes the project id, so the menu page
 * and the project detail page can both show it.
 */
@Component({
    selector: 'app-meeting-list',
    imports: [EmptyStateComponent, PaginationComponent, DateOnlyPipe, MeetingDetailComponent, MeetingFormComponent],
    templateUrl: './meeting-list.component.html',
    styleUrl: './meeting-list.component.scss',
})
export class MeetingListComponent {
    private readonly meetingService = inject(MeetingService);

    readonly projectId = input.required<number>();

    /** True when the project is not active: minutes can be read but not added or changed. */
    readonly readOnly = input(false);

    /** The record to open: ?id= in the address (docs/record-links.md). */
    readonly openId = input<number | null>(null);

    /** The opened record was closed, so the page can remove ?id= from the address. */
    readonly recordClosed = output<void>();

    constructor() {
        effect(() => {
            const id = this.openId();
            if (id !== null) untracked(() => this.panel.set({ kind: 'detail', meetingId: id }));
        });
    }

    protected readonly typeLabels = MEETING_TYPE_LABELS;
    protected readonly pageSize = 20;
    protected readonly page = signal(1);
    protected readonly panel = signal<Panel | null>(null);

    protected readonly detailId = computed(() => { const p = this.panel(); return p?.kind === 'detail' ? p.meetingId : null; });
    protected readonly editId = computed(() => { const p = this.panel(); return p?.kind === 'edit' ? p.meetingId : null; });
    protected readonly creating = computed(() => this.panel()?.kind === 'new');

    protected readonly meetings = rxResource({
        params: () => ({ projectId: this.projectId(), page: this.page() }),
        stream: ({ params }) => this.meetingService.list(params.projectId, params.page, this.pageSize),
    });

    /** After saving, the list is refreshed and the saved minutes are shown. */
    protected onSaved(meetingId: number): void {
        this.meetings.reload();
        this.panel.set({ kind: 'detail', meetingId });
    }

    protected closeDetail(): void {
        this.panel.set(null);
        this.recordClosed.emit();
    }
}
