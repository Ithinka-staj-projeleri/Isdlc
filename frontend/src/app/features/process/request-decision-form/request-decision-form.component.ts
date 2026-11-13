import { Component, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../../core/auth/auth.service';
import { DECISION_REASONS, DecisionReason, RequestDecisionForm } from '../models/request-decision';

/** What the form is filled in for: it decides the title, the date field and the button. */
export type DecisionFormMode = 'reject' | 'postpone' | 'close';

/** The filled-in form; reevaluationDate only for a postponement. */
export interface DecisionFormResult {
    decision: RequestDecisionForm;
    reevaluationDate: string | null;
}

/** Today in the user's time zone as "yyyy-MM-dd". */
function today(): string {
    const now = new Date();
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`;
}

/**
 * The Talep Ret / Erteleme Formu (ISDLC-Form-Analiz-007): karar bilgileri, gerekçe and, for a postponement,
 * the re-evaluation date. The request's own fields (no, name, date, owner) are already on the project.
 * Rules are checked here first, with the API's messages; the API checks them again.
 */
@Component({
    selector: 'app-request-decision-form',
    imports: [FormsModule],
    templateUrl: './request-decision-form.component.html',
    styleUrl: './request-decision-form.component.scss',
})
export class RequestDecisionFormComponent {
    readonly mode = input.required<DecisionFormMode>();
    readonly busy = input(false);

    readonly submitted = output<DecisionFormResult>();
    readonly cancelled = output<void>();

    protected readonly reasons = DECISION_REASONS;

    protected readonly decisionDate = signal(today());
    // The person filling in the form decides by default; a board can be written instead.
    protected readonly decidedBy = signal(inject(AuthService).user()?.fullName ?? '');
    protected readonly chosen = signal<ReadonlySet<DecisionReason>>(new Set());
    protected readonly reasonOther = signal('');
    protected readonly explanation = signal('');
    protected readonly reevaluationDate = signal('');
    protected readonly tried = signal(false);

    protected readonly title = computed(() =>
        this.mode() === 'postpone' ? 'Talep Erteleme Formu' : 'Talep Ret Formu');

    protected readonly submitLabel = computed(() => {
        switch (this.mode()) {
            case 'postpone': return 'Talebi ertele';
            case 'close': return 'Formu kaydet ve talebi kapat';
            default: return 'Talebi reddet';
        }
    });

    /** Turkish messages per field, the same texts the API uses; empty when the form is valid. */
    protected readonly errors = computed(() => {
        const errors: Partial<Record<'decisionDate' | 'decidedBy' | 'reasons' | 'reasonOther' | 'explanation' | 'reevaluationDate', string>> = {};
        if (!this.decisionDate()) errors.decisionDate = 'Karar tarihi zorunludur.';
        if (!this.decidedBy().trim()) errors.decidedBy = 'Kararı veren kişi ya da kurul zorunludur.';
        if (this.chosen().size === 0) errors.reasons = 'En az bir gerekçe kategorisi seçilmelidir.';
        if (this.chosen().has('Other') && !this.reasonOther().trim()) errors.reasonOther = 'Diğer gerekçe yazılmalıdır.';
        if (!this.explanation().trim()) errors.explanation = 'Gerekçe açıklaması zorunludur.';
        if (this.mode() === 'postpone') {
            if (!this.reevaluationDate()) {
                errors.reevaluationDate = 'Yeniden değerlendirme tarihi zorunludur.';
            } else if (this.decisionDate() && this.reevaluationDate() <= this.decisionDate()) {
                errors.reevaluationDate = 'Yeniden değerlendirme tarihi karar tarihinden sonra olmalıdır.';
            }
        }
        return errors;
    });

    /** Errors appear once the user has tried to send the form. */
    protected readonly shown = computed(() => (this.tried() ? this.errors() : {}));

    protected toggle(reason: DecisionReason, checked: boolean): void {
        const next = new Set(this.chosen());
        if (checked) next.add(reason); else next.delete(reason);
        this.chosen.set(next);
    }

    protected submit(): void {
        this.tried.set(true);
        if (Object.keys(this.errors()).length > 0) {
            return;
        }

        // In the form's order, whatever order they were ticked in.
        const reasons = DECISION_REASONS.map(r => r.value).filter(value => this.chosen().has(value));
        this.submitted.emit({
            decision: {
                decisionDate: this.decisionDate(),
                decidedBy: this.decidedBy().trim(),
                reasons,
                reasonOther: this.chosen().has('Other') ? this.reasonOther().trim() : null,
                explanation: this.explanation().trim(),
            },
            reevaluationDate: this.mode() === 'postpone' ? this.reevaluationDate() : null,
        });
    }
}
