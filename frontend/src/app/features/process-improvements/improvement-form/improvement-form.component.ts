import { Component, OnInit, inject, input, output, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { catchError, of } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { fieldErrorsOf } from '../../../core/http/problem-details';
import { LookupService } from '../../../shared/services/lookup.service';
import { IMPROVEMENT_SOURCE_LABELS, Improvement, ImprovementInput, ImprovementSource } from '../models/process-improvement';
import { ProcessImprovementService } from '../process-improvement.service';

/** Field → [limit, label] for the form's own length check (the API checks the same). */
const LIMITS: Record<string, [number, string]> = {
    title: [200, 'Başlık'],
    problem: [2000, 'Sorunun tanımı'],
    affectedProcess: [200, 'Etkilediği süreç'],
    proposedSolution: [2000, 'Çözüm fikri'],
    suggestedBy: [200, 'Öneriyi getiren'],
};

const REQUIRED: Record<string, string> = {
    title: 'Başlık zorunludur.',
    problem: 'Sorunun tanımı zorunludur.',
    affectedProcess: 'Etkilediği süreç zorunludur.',
    suggestedBy: 'Öneriyi getiren kişi zorunludur.',
};

/**
 * New proposal, or editing section 1 of one (title, problem, source, affected process, proposed solution,
 * who brought it, who runs it). A new proposal starts at step 1 of the process improvement flow.
 */
@Component({
    selector: 'app-improvement-form',
    imports: [ReactiveFormsModule],
    templateUrl: './improvement-form.component.html',
    styleUrl: './improvement-form.component.scss',
})
export class ImprovementFormComponent implements OnInit {
    private readonly service = inject(ProcessImprovementService);
    private readonly fb = inject(FormBuilder);

    /** null opens a new proposal. */
    readonly improvement = input<Improvement | null>(null);

    readonly saved = output<Improvement>();
    readonly cancelled = output<void>();

    protected readonly sourceLabels = IMPROVEMENT_SOURCE_LABELS;
    protected readonly sources = Object.keys(IMPROVEMENT_SOURCE_LABELS) as ImprovementSource[];
    // A failed load leaves the list empty; the error interceptor has already told the user.
    protected readonly users = toSignal(inject(LookupService).users$.pipe(catchError(() => of([]))), { initialValue: [] });

    protected readonly form = this.fb.nonNullable.group({
        title: [''],
        problem: [''],
        source: this.fb.control<ImprovementSource | null>(null),
        affectedProcess: [''],
        proposedSolution: [''],
        suggestedBy: [''],
        // Whoever opens the proposal usually runs it.
        ownerUserId: this.fb.control<number | null>(inject(AuthService).user()?.id ?? null),
    });

    protected readonly saving = signal(false);
    protected readonly submitted = signal(false);
    protected readonly serverErrors = signal<Record<string, string[]>>({});

    ngOnInit(): void {
        const current = this.improvement();
        if (current) {
            this.form.setValue({
                title: current.title,
                problem: current.problem,
                source: current.source,
                affectedProcess: current.affectedProcess,
                proposedSolution: current.proposedSolution ?? '',
                suggestedBy: current.suggestedBy,
                ownerUserId: current.owner?.id ?? null,
            });
        }
    }

    /** The API's first message for the field, then the form's own check after a save attempt. */
    protected errorFor(field: string): string | null {
        const fromServer = this.serverErrors()[field];
        if (fromServer?.length) {
            return fromServer[0];
        }
        if (!this.submitted()) {
            return null;
        }

        const value = this.form.getRawValue() as Record<string, unknown>;
        if (field === 'source') return value['source'] ? null : 'Önerinin kaynağı seçilmelidir.';
        if (field === 'ownerUserId') return value['ownerUserId'] !== null ? null : 'Öneriyi yürütecek kişi seçilmelidir.';

        const text = String(value[field] ?? '');
        if (REQUIRED[field] && !text.trim()) return REQUIRED[field];
        const [limit, label] = LIMITS[field] ?? [Infinity, ''];
        return text.length > limit ? `${label} en fazla ${limit} karakter olabilir.` : null;
    }

    protected save(): void {
        this.submitted.set(true);
        this.serverErrors.set({});
        const fields = [...Object.keys(LIMITS), 'source', 'ownerUserId'];
        if (fields.some(f => this.errorFor(f))) {
            return;
        }

        const v = this.form.getRawValue();
        const input: ImprovementInput = {
            title: v.title.trim(),
            problem: v.problem.trim(),
            source: v.source!,
            affectedProcess: v.affectedProcess.trim(),
            proposedSolution: v.proposedSolution.trim() || null,
            suggestedBy: v.suggestedBy.trim(),
            ownerUserId: v.ownerUserId!,
        };

        const current = this.improvement();
        this.saving.set(true);
        (current ? this.service.update(current.id, input) : this.service.create(input)).subscribe({
            next: improvement => {
                this.saving.set(false);
                this.saved.emit(improvement);
            },
            error: err => {
                this.saving.set(false);
                this.serverErrors.set(fieldErrorsOf(err));
            },
        });
    }
}
