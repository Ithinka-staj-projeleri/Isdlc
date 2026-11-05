import { Component, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Observable } from 'rxjs';
import { ConfirmDialogComponent } from '../../../shared/components/confirm-dialog/confirm-dialog.component';
import { PROJECT_STATUS_LABELS } from '../../../shared/models/project-status';
import { ProcessStep } from '../models/process-stage';
import { AvailableActions, ProjectProcess } from '../models/project-process';
import { ProcessService } from '../process.service';

const MAX_TEXT = 1000;

/** A command waiting for the user to confirm it in the dialog. */
interface PendingConfirm {
    title: string;
    message: string;
    run: () => Observable<ProjectProcess>;
}

/**
 * The action box under the current step: complete, Yes / No, reject.
 * Which buttons appear comes from the API (availableActions). Every command sends the step shown here
 * (expectedStepId), so a project moved on by someone else meanwhile answers 409 instead of being changed twice.
 */
@Component({
    selector: 'app-step-actions',
    imports: [FormsModule, ConfirmDialogComponent],
    templateUrl: './step-actions.component.html',
    styleUrl: './step-actions.component.scss',
})
export class StepActionsComponent {
    private readonly processService = inject(ProcessService);

    readonly projectId = input.required<number>();
    readonly step = input.required<ProcessStep>();
    readonly actions = input.required<AvailableActions>();

    /** The command succeeded; the new process state. */
    readonly updated = output<ProjectProcess>();
    /** The command failed (the error interceptor has shown why); the parent loads the current state again. */
    readonly failed = output<void>();

    protected readonly statusLabels = PROJECT_STATUS_LABELS;
    protected readonly maxText = MAX_TEXT;

    protected readonly note = signal('');
    protected readonly rejecting = signal(false);
    protected readonly reason = signal('');
    protected readonly reasonMissing = signal(false);
    protected readonly busy = signal(false);
    protected readonly pending = signal<PendingConfirm | null>(null);

    protected readonly canWork = computed(() => this.actions().canComplete || this.actions().canAnswer);

    /** "No" neither closes the request nor goes back: the step stays and is done again. */
    protected readonly noStays = computed(() => !this.step().noOutcome && this.step().noJumps.length === 0);

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

    /** A "No" that closes the request cannot be undone, so it is confirmed first. */
    protected answerNoAndClose(): void {
        const outcome = this.step().noOutcome!;
        this.pending.set({
            title: 'Talep kapanacak',
            message: `"Hayır" cevabıyla talep "${this.statusLabels[outcome]}" olarak kapanır ve süreçte başka işlem yapılamaz. Devam edilsin mi?`,
            run: () => this.answer('No', null),
        });
    }

    protected startReject(): void {
        this.rejecting.set(true);
        this.reasonMissing.set(false);
    }

    protected cancelReject(): void {
        this.rejecting.set(false);
        this.reason.set('');
        this.reasonMissing.set(false);
    }

    protected confirmReject(): void {
        const reason = this.reason().trim();
        if (!reason) {
            this.reasonMissing.set(true);
            return;
        }

        this.pending.set({
            title: 'Talep reddedilecek',
            message: 'Talep "Reddedildi" olarak kapanır ve süreçte başka işlem yapılamaz. Devam edilsin mi?',
            run: () => this.processService.reject(this.projectId(), this.step().id, reason),
        });
    }

    protected onConfirmed(): void {
        const pending = this.pending();
        this.pending.set(null);
        if (pending) {
            this.send(pending.run());
        }
    }

    private answer(answer: 'Yes' | 'No', jumpTargetStepId: number | null): Observable<ProjectProcess> {
        return this.processService.answer(this.projectId(), {
            expectedStepId: this.step().id,
            answer,
            jumpTargetStepId,
            note: this.cleanNote(),
        });
    }

    private send(request: Observable<ProjectProcess>): void {
        this.busy.set(true);
        request.subscribe({
            next: process => {
                this.busy.set(false);
                this.note.set('');
                this.cancelReject();
                this.updated.emit(process);
            },
            error: () => {
                this.busy.set(false);
                this.failed.emit();
            },
        });
    }
}
