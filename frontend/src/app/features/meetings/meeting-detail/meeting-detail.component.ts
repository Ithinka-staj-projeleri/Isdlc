import { Component, inject, input, output } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { BadgeTone, StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';
import { DateOnlyPipe } from '../../../shared/pipes/date-only.pipe';
import { LocalDatePipe } from '../../../shared/pipes/local-date-pipe';
import { OPEN_ISSUE_STATUS_LABELS, OpenIssueStatus } from '../../open-issues/models/open-issue';
import { MeetingService } from '../meeting.service';
import { MEETING_TYPE_LABELS } from '../models/meeting';

const STATUS_TONES: Record<OpenIssueStatus, BadgeTone> = { Open: 'info', InProgress: 'warn', Closed: 'neutral' };

/** The minutes with their decisions and actions, as open issues in their current status. */
@Component({
    selector: 'app-meeting-detail',
    imports: [RouterLink, StatusBadgeComponent, DateOnlyPipe, LocalDatePipe],
    templateUrl: './meeting-detail.component.html',
    styleUrl: './meeting-detail.component.scss',
})
export class MeetingDetailComponent {
    private readonly meetingService = inject(MeetingService);

    readonly projectId = input.required<number>();
    readonly meetingId = input.required<number>();
    readonly readOnly = input(false);

    readonly edit = output<void>();
    readonly closed = output<void>();

    protected readonly typeLabels = MEETING_TYPE_LABELS;
    protected readonly statusLabels = OPEN_ISSUE_STATUS_LABELS;
    protected readonly statusTones = STATUS_TONES;

    protected readonly meeting = rxResource({
        params: () => ({ projectId: this.projectId(), meetingId: this.meetingId() }),
        stream: ({ params }) => this.meetingService.get(params.projectId, params.meetingId),
    });
}
