import { Component, OnInit, computed, inject, input, output, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { catchError, of } from 'rxjs';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { AuthService } from '../../../core/auth/auth.service';
import { fieldErrorsOf } from '../../../core/http/problem-details';
import { LookupService } from '../../../shared/services/lookup.service';
import { MeetingSuggestions, SuggestedAction } from '../../agents/models/meeting-suggestions';
import { ProcessService } from '../../process/process.service';
import { ProjectProcess } from '../../process/models/project-process';
import { MeetingAssistantComponent } from '../meeting-assistant/meeting-assistant.component';
import { MeetingService } from '../meeting.service';
import {
    CreateMeeting,
    MEETING_TYPE_LABELS,
    Meeting,
    MeetingAction,
    MeetingParticipant,
    MeetingType,
    UpdateMeeting,
    VISIT_TYPE,
} from '../models/meeting';

type OwnerKind = 'user' | 'name';

const MAX_ROWS = 50;
const MAX_PARTICIPANTS = 30;
const MAX_DECISION_LENGTH = 300;

/** Text fields and their limits (docs/open-issues-meetings-api.md → Faz 4 · E4). */
const TEXT_LIMITS: Record<string, { limit: number; label: string }> = {
    agenda: { limit: 4000, label: 'Gündem' },
    summary: { limit: 4000, label: 'Toplantı özeti' },
    completedStages: { limit: 2000, label: 'Tamamlanan aşamalar' },
    ongoingStages: { limit: 2000, label: 'Devam eden aşamalar' },
    nextSteps: { limit: 2000, label: 'Sonraki adımlar' },
    plannedActivities: { limit: 4000, label: 'Planlanan aktiviteler' },
    completedActivities: { limit: 4000, label: 'Yapılan aktiviteler' },
};

/** Today as a calendar date in the user's time zone ("2026-11-05"). */
function today(): string {
    const now = new Date();
    return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`;
}

/** Whitespace-only text is sent as null. */
function clean(value: string): string | null {
    return value.trim() || null;
}

/** "Talep ve ön analiz, Teklif ve anlaşma" and "Proje başlatma (12. adım)" from where the project stands. */
function stagesOf(process: ProjectProcess): { completed: string; ongoing: string } {
    const completed = process.stages.filter(s => s.state === 'Completed').map(s => s.shortTitle).join(', ');
    const current = process.stages.find(s => s.state === 'Current');
    const step = process.currentStep ? ` (${process.currentStep.code}. adım)` : '';
    return { completed, ongoing: current ? current.shortTitle + step : '' };
}

/**
 * New minutes (with decisions and actions, which become open issues) or editing the minutes' info.
 * Decisions and actions of saved minutes are managed on the Open Issues screen, so editing shows only the info.
 * A customer visit adds the return date and the planned / completed activities.
 */
@Component({
    selector: 'app-meeting-form',
    imports: [ReactiveFormsModule, MeetingAssistantComponent],
    templateUrl: './meeting-form.component.html',
    styleUrl: './meeting-form.component.scss',
})
export class MeetingFormComponent implements OnInit {
    private readonly meetingService = inject(MeetingService);
    private readonly processService = inject(ProcessService);
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
    protected readonly maxParticipants = MAX_PARTICIPANTS;

    protected readonly form = this.fb.nonNullable.group({
        type: this.fb.nonNullable.control<MeetingType>('AnalysisSession'),
        date: [today()],
        returnDate: [''],
        // The person who writes the minutes is usually the one signed in.
        recordedByUserId: this.fb.control<number | null>(inject(AuthService).user()?.id ?? null),
        participants: this.fb.array<FormGroup>([]),
        agenda: [''],
        summary: [''],
        completedStages: [''],
        ongoingStages: [''],
        nextSteps: [''],
        plannedActivities: [''],
        completedActivities: [''],
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

    private readonly type = toSignal(this.form.controls.type.valueChanges, { initialValue: this.form.controls.type.value });
    protected readonly isVisit = computed(() => this.type() === VISIT_TYPE);

    /** Decision lines the AI assistant added, so asking again replaces them instead of adding them twice. */
    private agentDecisionLines: string[] = [];

    /** The info fields as the AI assistant last filled them; a field still showing that value may be replaced again. */
    private agentInfo: { type: MeetingType | null; participants: string | null; agenda: string | null } =
        { type: null, participants: null, agenda: null };

    protected get actions(): FormArray<FormGroup> {
        return this.form.controls.actions;
    }

    protected get participants(): FormArray<FormGroup> {
        return this.form.controls.participants;
    }

    ngOnInit(): void {
        const id = this.meetingId();
        if (id === null) {
            this.addParticipant();
            this.suggestStages();
            return;
        }

        this.loading.set(true);
        this.meetingService.get(this.projectId(), id).subscribe({
            next: meeting => {
                this.code.set(meeting.code);
                meeting.participants.forEach(p => this.addParticipant(p));
                this.form.patchValue({
                    type: meeting.type,
                    date: meeting.date,
                    returnDate: meeting.returnDate ?? '',
                    recordedByUserId: meeting.recordedBy.id,
                    agenda: meeting.agenda,
                    summary: meeting.summary ?? '',
                    completedStages: meeting.completedStages ?? '',
                    ongoingStages: meeting.ongoingStages ?? '',
                    nextSteps: meeting.nextSteps ?? '',
                    plannedActivities: meeting.plannedActivities ?? '',
                    completedActivities: meeting.completedActivities ?? '',
                });
                this.loading.set(false);
            },
            error: () => this.cancelled.emit(),
        });
    }

    /**
     * New minutes: the stages are suggested from where the project stands now. They are kept as written,
     * so later minutes show their own moment. Without the process (error) the fields stay empty.
     */
    private suggestStages(): void {
        this.processService.getProjectProcess(this.projectId()).pipe(catchError(() => of(null))).subscribe(process => {
            if (!process) {
                return;
            }

            const { completed, ongoing } = stagesOf(process);
            const { completedStages, ongoingStages } = this.form.controls;
            if (!completedStages.value.trim()) completedStages.setValue(completed);
            if (!ongoingStages.value.trim()) ongoingStages.setValue(ongoing);
        });
    }

    protected addParticipant(participant?: MeetingParticipant): void {
        if (this.participants.length >= MAX_PARTICIPANTS) {
            return;
        }

        this.participants.push(this.fb.nonNullable.group({
            name: [participant?.name ?? ''],
            role: [participant?.role ?? ''],
        }));
    }

    protected removeParticipant(index: number): void {
        this.participants.removeAt(index);
        // Row numbers in the API's messages ("participants[2].name") no longer match the rows.
        this.serverErrors.set({});
    }

    /** Names in the rows, "Elif Yılmaz, Can Öztürk", to compare with what the AI assistant filled before. */
    private participantNames(): string {
        return this.participants.controls.map(c => (c.value.name as string).trim()).filter(n => n).join(', ');
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

        this.applyMinutesDraft(suggestions);
        this.serverErrors.set({});
    }

    /**
     * Type, participants and agenda from the AI draft, only where the person has not written anything:
     * no names in the rows, a type left as it was, or a value the assistant itself put there before.
     * The draft's participants come as "Ad, Ad (Kurum)" and become rows without a role.
     */
    private applyMinutesDraft(suggestions: MeetingSuggestions): void {
        const draft = suggestions.minutes;
        if (!draft) {
            return;
        }

        const { type, agenda } = this.form.controls;
        if (type.pristine || type.value === this.agentInfo.type) {
            type.setValue(draft.type);
            this.agentInfo.type = draft.type;
        }

        const names = this.participantNames();
        if (!names || names === this.agentInfo.participants) {
            this.participants.clear();
            draft.participants.split(',').map(n => n.trim()).filter(n => n).slice(0, MAX_PARTICIPANTS)
                .forEach(name => this.addParticipant({ name, role: null }));
            this.agentInfo.participants = this.participantNames();
        }

        if (!agenda.value.trim() || agenda.value === this.agentInfo.agenda) {
            agenda.setValue(draft.agenda);
            this.agentInfo.agenda = draft.agenda;
        }
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

    /** Rows with a name; empty rows are dropped instead of being an error. */
    private filledParticipants(): MeetingParticipant[] {
        return this.participants.controls
            .map(c => ({ name: (c.value.name as string).trim(), role: clean(c.value.role as string) }))
            .filter(p => p.name);
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
                return value.date ? null : (this.isVisit() ? 'Gidiş tarihi zorunludur.' : 'Toplantı tarihi zorunludur.');
            case 'returnDate':
                if (!this.isVisit()) return null;
                if (!value.returnDate) return 'Ziyarette dönüş tarihi zorunludur.';
                return value.date && value.returnDate < value.date ? 'Dönüş tarihi gidiş tarihinden önce olamaz.' : null;
            case 'recordedByUserId':
                return value.recordedByUserId === null ? 'Tutanağı yazan kişi seçilmelidir.' : null;
            case 'participants':
                return this.filledParticipants().length ? null : 'En az bir katılımcı girilmelidir.';
            case 'agenda':
                if (!value.agenda.trim()) return 'Gündem zorunludur.';
                return this.tooLong(field, value.agenda);
            case 'decisions': {
                const lines = this.decisionLines();
                if (lines.length > MAX_ROWS) return `En fazla ${MAX_ROWS} karar girilebilir.`;
                const long = lines.findIndex(line => line.length > MAX_DECISION_LENGTH);
                return long >= 0 ? `${long + 1}. karar en fazla ${MAX_DECISION_LENGTH} karakter olabilir.` : null;
            }
            default:
                return field in TEXT_LIMITS ? this.tooLong(field, (value as Record<string, unknown>)[field] as string) : null;
        }
    }

    private tooLong(field: string, value: string): string | null {
        const { limit, label } = TEXT_LIMITS[field];
        return value.length > limit ? `${label} en fazla ${limit} karakter olabilir.` : null;
    }

    /** Errors of one participant row (only the API's: empty rows are dropped, lengths are capped by the inputs). */
    protected participantError(index: number): string | null {
        const server = this.serverErrors();
        return (server[`participants[${index}].name`] ?? server[`participants[${index}].role`])?.[0] ?? null;
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
        const fields = ['date', 'returnDate', 'recordedByUserId', 'participants', ...Object.keys(TEXT_LIMITS)];
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
        const visit = this.isVisit();
        const info: UpdateMeeting = {
            type: value.type,
            date: value.date,
            // Visit fields are cleared when the type is not a visit, so changing the type does not leave them behind.
            returnDate: visit ? value.returnDate || null : null,
            recordedByUserId: value.recordedByUserId!,
            participants: this.filledParticipants(),
            agenda: value.agenda.trim(),
            summary: clean(value.summary),
            completedStages: clean(value.completedStages),
            ongoingStages: clean(value.ongoingStages),
            nextSteps: clean(value.nextSteps),
            plannedActivities: visit ? clean(value.plannedActivities) : null,
            completedActivities: visit ? clean(value.completedActivities) : null,
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
