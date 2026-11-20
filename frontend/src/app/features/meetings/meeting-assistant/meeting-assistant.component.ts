import { Component, computed, inject, input, output, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { catchError, of } from 'rxjs';
import { DateOnlyPipe } from '../../../shared/pipes/date-only.pipe';
import { LookupService } from '../../../shared/services/lookup.service';
import { AgentService } from '../../agents/agent.service';
import { MeetingSuggestions } from '../../agents/models/meeting-suggestions';
import { OPEN_ISSUE_TYPE_LABELS } from '../../open-issues/models/open-issue';
import { MEETING_TYPE_LABELS } from '../models/meeting';

const MAX_NOTES = 20_000;

/** The suggested actions of one person (or of nobody: owner null). */
interface OwnerActions {
    owner: string | null;
    isUser: boolean;
    titles: string[];
}

/**
 * "Yapay zekâ ile karar ve aksiyonları çıkar" in the new minutes form. Sends the raw notes, hands the
 * suggestions to the form (suggested), and shows what the form has no place for. Nothing is saved here.
 * Hidden when the AI service is off.
 */
@Component({
    selector: 'app-meeting-assistant',
    imports: [FormsModule, DateOnlyPipe],
    templateUrl: './meeting-assistant.component.html',
    styleUrl: './meeting-assistant.component.scss',
})
export class MeetingAssistantComponent {
    private readonly agentService = inject(AgentService);

    readonly projectId = input.required<number>();
    /** The meeting date in the form; "gelecek çarşamba" style due dates are worked out from it. */
    readonly meetingDate = input.required<string>();

    readonly suggested = output<MeetingSuggestions>();

    protected readonly enabled = toSignal(this.agentService.meetingAssistantEnabled(), { initialValue: false });
    protected readonly typeLabels = OPEN_ISSUE_TYPE_LABELS;
    protected readonly meetingTypeLabels = MEETING_TYPE_LABELS;
    protected readonly maxNotes = MAX_NOTES;

    protected readonly notes = signal('');
    protected readonly busy = signal(false);
    protected readonly result = signal<MeetingSuggestions | null>(null);

    private readonly users = toSignal(inject(LookupService).users$.pipe(catchError(() => of([]))), { initialValue: [] });

    /** Who does what: most actions first, actions without an owner last. */
    protected readonly byOwner = computed<OwnerActions[]>(() => {
        const groups = new Map<string, OwnerActions>();
        for (const action of this.result()?.actions ?? []) {
            const userName = action.ownerUserId === null
                ? null
                : (this.users().find(u => u.id === action.ownerUserId)?.fullName ?? null);
            const owner = userName ?? action.ownerName;
            const key = owner?.toLocaleLowerCase('tr') ?? '';
            const group = groups.get(key) ?? { owner, isUser: userName !== null, titles: [] };
            group.titles.push(action.title);
            groups.set(key, group);
        }
        return [...groups.values()].sort((a, b) =>
            (a.owner === null ? 1 : 0) - (b.owner === null ? 1 : 0)
            || b.titles.length - a.titles.length
            || (a.owner ?? '').localeCompare(b.owner ?? '', 'tr'));
    });

    protected ask(): void {
        const notes = this.notes().trim();
        if (!notes || !this.meetingDate() || this.busy()) {
            return;
        }

        this.busy.set(true);
        this.agentService.suggestMeetingItems(this.projectId(), this.meetingDate(), notes).subscribe({
            next: suggestions => {
                this.busy.set(false);
                this.result.set(suggestions);
                this.suggested.emit(suggestions);
            },
            // The error interceptor shows the API's message (service busy, quota...).
            error: () => this.busy.set(false),
        });
    }
}
