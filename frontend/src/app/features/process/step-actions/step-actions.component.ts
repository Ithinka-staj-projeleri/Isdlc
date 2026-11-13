import { Component, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Observable } from 'rxjs';
import { ConfirmDialogComponent } from '../../../shared/components/confirm-dialog/confirm-dialog.component';
import { PROJECT_STATUS_LABELS } from '../../../shared/models/project-status';
import { ProcessFlowKind, ProcessStep } from '../models/process-stage';
import { AvailableActions, ProjectProcess } from '../models/project-process';
import { RequestDecisionForm } from '../models/request-decision';
import { ProcessService } from '../process.service';
import {
    DecisionFormMode,
    DecisionFormResult,
    RequestDecisionFormComponent,
} from '../request-decision-form/request-decision-form.component';

const MAX_TEXT = 1000;

/** A command waiting for the user to confirm it in the dialog. */
interface PendingConfirm {
    title: string;
    message: string;
    run: () => Observable<ProjectProcess>;
}

/** "2027-01-15" → "15.01.2027". */
function formatDate(value: string): string {
    const [year, month, day] = value.split('-');
    return `${day}.${month}.${year}`;
}

/**
 * The action box under the current step: complete, Yes / No, reject, postpone, take back.
 * Which buttons appear comes from the API (availableActions). Every command sends the step shown here
 * (expectedStepId), so a project moved on by someone else meanwhile answers 409 instead of being changed twice.
 * Rejecting, postponing and a "No" that closes a request are recorded with the Talep Ret / Erteleme Formu.
 */
@Component({
    selector: 'app-step-actions',
    imports: [FormsModule, ConfirmDialogComponent, RequestDecisionFormComponent],
    templateUrl: './step-actions.component.html',
    styleUrl: './step-actions.component.scss',
})
export class StepActionsComponent {
    private readonly processService = inject(ProcessService);

    readonly projectId = input.required<number>();
    readonly step = input.required<ProcessStep>();
    readonly actions = input.required<AvailableActions>();
    /** The kind of the project's flow: only a request's closing "No" needs the form. */
    readonly flowKind = input.required<ProcessFlowKind>();

    /** The command succeeded; the new process state. */
    readonly updated = output<ProjectProcess>();
    /** The command failed (the error interceptor has shown why); the parent loads the current state again. */
    readonly failed = output<void>();

    protected readonly statusLabels = PROJECT_STATUS_LABELS;
    protected readonly maxText = MAX_TEXT;

    protected readonly note = signal('');
    protected readonly resumeNote = signal('');
    /** The open Talep Ret / Erteleme Formu, or null. */
    protected readonly decisionMode = signal<DecisionFormMode | null>(null);
    protected readonly busy = signal(false);
    protected readonly pending = signal<PendingConfirm | null>(null);

    protected readonly canWork = computed(() => this.actions().canComplete || this.actions().canAnswer);

    /** "No" neither closes the request nor goes back: the step stays and is done again. */
    protected readonly noStays = computed(() => !this.step().noOutcome && this.step().noJumps.length === 0);

    /** A "No" that closes a request is recorded with the form (yeni proje 4 and 11). */
    private readonly closingNeedsForm = computed(() => !!this.step().noOutcome && this.flowKind() === 'Request');

    private cleanNote(): string | null {
        return this.note().trim() || null;
    }

    protected complete(): void {
        this.send(this.processService.complete(this.projectId(), this.step().id, this.cleanNote()));
    }

    protected answerYes(): void {
        this.send(this.answer('Yes', null));
    }

    protected answerNo(jumpTargetStepId: number | null): void {
        this.send(this.answer('No', jumpTargetStepId));
    }

    /** A "No" that closes the request cannot be undone: the form first (for a request), then a confirmation. */
    protected answerNoAndClose(): void {
        if (this.closingNeedsForm()) {
            this.decisionMode.set('close');
            return;
        }

        const outcome = this.step().noOutcome!;
        this.pending.set({
            title: 'Kayıt kapanacak',
            message: `"Hayır" cevabıyla kayıt "${this.statusLabels[outcome]}" olarak kapanır ve süreçte başka işlem yapılamaz. Devam edilsin mi?`,
            run: () => this.answer('No', null),
        });
    }

    protected startDecision(mode: DecisionFormMode): void {
        this.decisionMode.set(mode);
    }

    protected cancelDecision(): void {
        this.decisionMode.set(null);
    }

    /** The form is filled in: confirm what it will do, then send it. */
    protected onDecision(result: DecisionFormResult): void {
        const projectId = this.projectId();
        const stepId = this.step().id;

        switch (this.decisionMode()) {
            case 'postpone':
                this.pending.set({
                    title: 'Talep ertelenecek',
                    message: `Talep "Ertelendi" olarak bu adımda bekler ve ${formatDate(result.reevaluationDate!)} tarihinde yeniden değerlendirilir. Değerlendirmeye geri alınana kadar süreçte işlem yapılamaz. Devam edilsin mi?`,
                    run: () => this.processService.postpone(projectId, stepId, result.decision, result.reevaluationDate!),
                });
                break;
            case 'close': {
                const outcome = this.step().noOutcome!;
                this.pending.set({
                    title: 'Talep kapanacak',
                    message: `"Hayır" cevabıyla talep "${this.statusLabels[outcome]}" olarak kapanır ve süreçte başka işlem yapılamaz. Devam edilsin mi?`,
                    run: () => this.answer('No', null, result.decision),
                });
                break;
            }
            default:
                this.pending.set({
                    title: 'Talep reddedilecek',
                    message: 'Talep "Reddedildi" olarak kapanır ve süreçte başka işlem yapılamaz. Devam edilsin mi?',
                    run: () => this.processService.reject(projectId, stepId, result.decision),
                });
        }
    }

    protected resume(): void {
        const note = this.resumeNote().trim() || null;
        this.pending.set({
            title: 'Talep değerlendirmeye geri alınacak',
            message: `Talep yeniden "Aktif" olur ve ${this.step().code}. adımdan devam eder. Devam edilsin mi?`,
            run: () => this.processService.resume(this.projectId(), this.step().id, note),
        });
    }

    protected onConfirmed(): void {
        const pending = this.pending();
        this.pending.set(null);
        if (pending) {
            this.send(pending.run());
        }
    }

    private answer(
        answer: 'Yes' | 'No', jumpTargetStepId: number | null, closingDecision: RequestDecisionForm | null = null,
    ): Observable<ProjectProcess> {
        return this.processService.answer(this.projectId(), {
            expectedStepId: this.step().id,
            answer,
            jumpTargetStepId,
            note: this.cleanNote(),
            closingDecision,
        });
    }

    private send(request: Observable<ProjectProcess>): void {
        this.busy.set(true);
        request.subscribe({
            next: process => {
                this.busy.set(false);
                this.note.set('');
                this.resumeNote.set('');
                this.decisionMode.set(null);
                this.updated.emit(process);
            },
            error: () => {
                this.busy.set(false);
                this.failed.emit();
            },
        });
    }
}
