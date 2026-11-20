import { Component, computed, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { PaginationComponent } from '../../../shared/components/pagination/pagination.component';
import { MEETING_TYPE_LABELS } from '../../meetings/models/meeting';
import { STEP_ACTION_LABELS } from '../../process/models/project-process';
import { AuditService } from '../audit.service';
import { TimelineEvent, TimelineEventKind } from '../models/timeline';

/** The project detail tab each kind of event opens. */
const TAB_OF: Record<TimelineEventKind, string | null> = {
    Created: 'general',
    Step: 'process',
    Meeting: 'meetings',
    Decision: 'open-issues',
    RequirementApproved: 'requirements',
    RiskClosed: 'risks',
};

interface TimelineDay {
    label: string;
    events: TimelineEvent[];
}

/**
 * Zaman Çizelgesi tab of the project detail page: what happened on the project, newest first, grouped by day.
 * Kayıt Geçmişi has every field change; this tab tells the story. Read only.
 */
@Component({
    selector: 'app-project-timeline',
    imports: [RouterLink, EmptyStateComponent, PaginationComponent],
    templateUrl: './project-timeline.component.html',
    styleUrl: './project-timeline.component.scss',
})
export class ProjectTimelineComponent {
    private readonly auditService = inject(AuditService);

    readonly projectId = input.required<number>();

    protected readonly pageSize = 50;
    protected readonly page = signal(1);

    protected readonly timeline = rxResource({
        params: () => ({ projectId: this.projectId(), page: this.page() }),
        stream: ({ params }) => this.auditService.projectTimeline(params.projectId, params.page, this.pageSize),
    });

    /** The page's events under day headings ("5 Kasım 2026"), in the order the API gives them. */
    protected readonly days = computed<TimelineDay[]>(() => {
        const days: TimelineDay[] = [];
        for (const event of this.timeline.hasValue() ? this.timeline.value().items : []) {
            const label = dayOf(event);
            const last = days.at(-1);
            if (last?.label === label) {
                last.events.push(event);
            } else {
                days.push({ label, events: [event] });
            }
        }
        return days;
    });

    protected heading(event: TimelineEvent): string {
        switch (event.kind) {
            case 'Created':
                return `Kayıt açıldı · ${event.recordCode}`;
            case 'Step': {
                const action = event.stepAction ? STEP_ACTION_LABELS[event.stepAction] : '';
                const jump = event.jumpedToStepCode ? ` → ${event.jumpedToStepCode}. adıma dönüldü` : '';
                return `Adım ${event.recordCode} · ${action}${jump}`;
            }
            case 'Meeting':
                return `Tutanak ${event.recordCode} · ${event.meetingType ? MEETING_TYPE_LABELS[event.meetingType] : ''}`;
            case 'Decision':
                return `Karar ${event.recordCode}`;
            case 'RequirementApproved':
                return `Gereksinim ${event.recordCode} onaylandı`;
            case 'RiskClosed':
                return `Risk ${event.recordCode} kapandı`;
        }
    }

    protected tabOf(event: TimelineEvent): string | null {
        return TAB_OF[event.kind];
    }

    protected timeOf(event: TimelineEvent): string {
        return event.dateOnly
            ? ''
            : new Date(event.occurredAt).toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' });
    }
}

/** A meeting's date is a day, not a moment: it is read as written, not moved into the local time zone. */
function dayOf(event: TimelineEvent): string {
    const date = event.dateOnly ? new Date(`${event.occurredAt.slice(0, 10)}T12:00:00`) : new Date(event.occurredAt);
    return date.toLocaleDateString('tr-TR', { day: 'numeric', month: 'long', year: 'numeric' });
}
