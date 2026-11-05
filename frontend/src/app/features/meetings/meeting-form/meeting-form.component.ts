import { Component, OnInit, inject, input, output, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { catchError, of } from 'rxjs';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { fieldErrorsOf } from '../../../core/http/problem-details';
import { LookupService } from '../../../shared/services/lookup.service';
import { MeetingSuggestions, SuggestedAction } from '../../agents/models/meeting-suggestions';
import { MeetingAssistantComponent } from '../meeting-assistant/meeting-assistant.component';
import { MeetingService } from '../meeting.service';
import { CreateMeeting, MEETING_TYPE_LABELS, Meeting, MeetingAction, MeetingType, UpdateMeeting } from '../models/meeting';

type OwnerKind = 'user' | 'name';

const MAX_ROWS = 50;
const MAX_DECISION_LENGTH = 300;

/** Today as a calendar date in the user's time zone ("2026-11-05"). */
function today(): string {
    const now = new Date();
    return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`;
}

/**
 * New minutes (with decisions and actions, which become open issues) or editing the minutes' info.
 * Decisions and actions of saved minutes are managed on the Open Issues screen, so editing shows only the info.
 */
@Component({
    selector: 'app-meeting-form',
    imports: [ReactiveFormsModule, MeetingAssistantComponent],
    templateUrl: './meeting-form.component.html',
    styleUrl: './meeting-form.component.scss',
})
export class MeetingFormComponent implements OnInit {
    private readonly meetingService = inject(MeetingService);
    private readonly fb = inject(FormBuilder);

    readonly projectId = input.required<number>();
    /** null creates new minutes. */
    readonly meetingId = input<number | null>(null);

    readonly saved = output<Meeting>();
    readonly cancelled = output<void>();

    protected readonly typeLabels = MEETING_TYPE_LABELS;
    protected readonly types = Object.keys(MEETING_TYPE_LABELS) as MeetingType[];
    // A failed load leaves the list empty; the error interceptor has already told the user.
    protected readonly users = toSignal(inject(LookupService).users$.pipe(catchError(() => of([]))), { initialValue: [] });
    protected readonly maxRows = MAX_ROWS;

    protected readonly form = this.fb.nonNullable.group({
        type: this.fb.nonNullable.control<MeetingType>('AnalysisSession'),
        date: [today()],
        participants: [''],
        agenda: [''],
        decisions: [''],
        actions: this.fb.array<FormGroup>([]),
    });

    protected readonly code = signal<string | null>(null);
    protected readonly loading = signal(false);
    protected readonly saving = signal(false);
    protected readonly submitted = signal(false);
    protected readonly serverErrors = signal<Record<string, string[]>>({});

    /** The date in the form, for the AI assistant (due dates like "gelecek çarşamba" are worked out from it). */
    protected readonly meetingDate = toSignal(this.form.controls.date.valueChanges, { initialValue: this.form.controls.date.value });

    /** Decision lines the AI assistant added, so asking again replaces them instead of adding them twice. */
    private agentDecisionLines: string[] = [];

    protected get actions(): FormArray<FormGroup> {
        return this.form.controls.actions;
    }

    ngOnInit(): void {
        const id = this.meetingId();
        if (id === null) {
            return;
        }

        this.loading.set(true);
        this.meetingService.get(this.projectId(), id).subscribe({
            next: meeting => {
                this.code.set(meeting.code);
                this.form.patchValue({
                    type: meeting.type,
                    date: meeting.date,
                    participants: meeting.participants,
                    agenda: meeting.agenda,
                });
                this.loading.set(false);
            },
            error: () => this.cancelled.emit(),
        });
    }

    /** An empty row, or one filled from an AI suggestion (then it also keeps the evidence and warnings to show). */
    protected addAction(suggestion?: SuggestedAction): void {
        if (this.actions.length >= MAX_ROWS) {
            return;
        }

        this.actions.push(this.fb.nonNullable.group({
            title: [suggestion?.title ?? ''],
            ownerKind: this.fb.nonNullable.control<OwnerKind>(suggestion && suggestion.ownerUserId === null && suggestion.ownerName ? 'name' : 'user'),
            ownerUserId: this.fb.control<number | null>(suggestion?.ownerUserId ?? null),
            ownerName: [suggestion?.ownerName ?? ''],
            dueDate: [suggestion?.dueDate ?? ''],
            // Shown under the row, never sent: null for rows the person added.
            evidence: this.fb.control<string | null>(suggestion?.evidence ?? null),
            warnings: this.fb.nonNullable.control<string[]>(suggestion?.warnings ?? []),
        }));
    }

    /**
     * Puts the AI suggestions into the form for review. Asking again replaces the earlier suggestions
     * (rows and decision lines the person typed stay; edited suggestion rows are replaced too).
     */
    protected applySuggestions(suggestions: MeetingSuggestions): void {
        for (let i = this.actions.length - 1; i >= 0; i--) {
            if (this.actions.at(i).controls['evidence'].value !== null) {
                this.actions.removeAt(i);
            }
        }
        suggestions.actions.forEach(action => this.addAction(action));

        const ownLines = this.form.controls.decisions.value
            .split('\n')
            .filter(line => line.trim() && !this.agentDecisionLines.includes(line.trim()));
        this.agentDecisionLines = suggestions.decisions.map(d => d.title.trim());
        this.form.controls.decisions.setValue([...ownLines, ...this.agentDecisionLines].join('\n'));

        this.serverErrors.set({});
    }

    protected removeAction(index: number): void {
        this.actions.removeAt(index);
        // Row numbers in the API's messages ("actions[2].title") no longer match the rows.
        this.serverErrors.set({});
    }

    /** Lines of the decisions box, blank ones skipped (the API skips them too). */
    private decisionLines(): string[] {
        return this.form.controls.decisions.value.split('\n').map(line => line.trim()).filter(line => line);
    }

    /** The first message for a field: the API's first, then the form's own check after a save attempt. */
    protected errorFor(field: string): string | null {
        const fromServer = this.serverErrors()[field];
        if (fromServer?.length) {
            return fromServer[0];
        }

        if (!this.submitted()) {
            return null;
        }

        const value = this.form.getRawValue();
        switch (field) {
            case 'date':
                return value.date ? null : 'Toplantı tarihi zorunludur.';
            case 'participants':
                if (!value.participants.trim()) return 'Katılımcılar zorunludur.';
                return value.participants.length > 1000 ? 'Katılımcılar en fazla 1000 karakter olabilir.' : null;
            case 'agenda':
                if (!value.agenda.trim()) return 'Gündem zorunludur.';
                return value.agenda.length > 4000 ? 'Gündem en fazla 4000 karakter olabilir.' : null;
            case 'decisions': {
                const lines = this.decisionLines();
                if (lines.length > MAX_ROWS) return `En fazla ${MAX_ROWS} karar girilebilir.`;
                const long = lines.findIndex(line => line.length > MAX_DECISION_LENGTH);
                return long >= 0 ? `${long + 1}. karar en fazla ${MAX_DECISION_LENGTH} karakter olabilir.` : null;
            }
            default:
                return null;
        }
    }

    /** Errors of one action row: its title and its owner. */
    protected actionError(index: number, part: 'title' | 'owner'): string | null {
        const server = this.serverErrors();
        const fromServer = part === 'title'
            ? server[`actions[${index}].title`]
            : server[`actions[${index}].ownerName`] ?? server[`actions[${index}].ownerUserId`];
        if (fromServer?.length) {
            return fromServer[0];
        }

        if (!this.submitted()) {
            return null;
        }

        const row = this.actions.at(index).getRawValue();
        if (part === 'title') {
            if (!row.title.trim()) return 'Aksiyon konusu zorunludur.';
            return row.title.length > 300 ? 'Aksiyon konusu en fazla 300 karakter olabilir.' : null;
        }

        if (row.ownerKind === 'user' && row.ownerUserId === null) return 'Sorumlu kişiyi seçin.';
        if (row.ownerKind === 'name' && !row.ownerName.trim()) return 'Sorumlunun adını yazın.';
        return null;
    }

    private hasErrors(): boolean {
        const fields = ['date', 'participants', 'agenda'];
        if (this.meetingId() === null) {
            fields.push('decisions');
        }

        return fields.some(f => this.errorFor(f))
            || (this.meetingId() === null
                && this.actions.controls.some((_, i) => this.actionError(i, 'title') || this.actionError(i, 'owner')));
    }

    protected save(): void {
        this.submitted.set(true);
        this.serverErrors.set({});
        if (this.hasErrors()) {
            return;
        }

        const value = this.form.getRawValue();
        const info: UpdateMeeting = {
            type: value.type,
            date: value.date,
            participants: value.participants.trim(),
            agenda: value.agenda.trim(),
        };

        const id = this.meetingId();
        let request;
        if (id === null) {
            const body: CreateMeeting = {
                ...info,
                decisions: this.decisionLines(),
                actions: this.actions.controls.map(control => {
                    const row = control.getRawValue();
                    return {
                        title: row.title.trim(),
                        ownerUserId: row.ownerKind === 'user' ? row.ownerUserId : null,
                        ownerName: row.ownerKind === 'name' ? row.ownerName.trim() : null,
                        dueDate: row.dueDate || null,
                    } satisfies MeetingAction;
                }),
            };
            request = this.meetingService.create(this.projectId(), body);
        } else {
            request = this.meetingService.update(this.projectId(), id, info);
        }

        this.saving.set(true);
        request.subscribe({
            next: meeting => {
                this.saving.set(false);
                this.saved.emit(meeting);
            },
            error: err => {
                this.saving.set(false);
                this.serverErrors.set(fieldErrorsOf(err));
            },
        });
    }
}
