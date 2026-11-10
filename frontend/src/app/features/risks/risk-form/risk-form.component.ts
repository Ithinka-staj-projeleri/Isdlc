import { Component, OnInit, computed, inject, input, output, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { catchError, of } from 'rxjs';
import { fieldErrorsOf } from '../../../core/http/problem-details';
import { StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';
import { LookupService } from '../../../shared/services/lookup.service';
import { AuthService } from '../../../core/auth/auth.service';
import {
    RISK_CATEGORY_LABELS, RISK_SEVERITY_LABELS, RISK_SEVERITY_TONES, RISK_STATUS_LABELS, Risk, RiskCategory, RiskStatus,
    SaveRisk, severityOf,
} from '../models/risk';
import { RiskService } from '../risk.service';

type Field = 'title' | 'description' | 'category' | 'probability' | 'impact' | 'mitigation' | 'ownerUserId' | 'status';

/** Add or edit a risk. The status can be changed only when editing; a new risk starts as Open. */
@Component({
    selector: 'app-risk-form',
    imports: [ReactiveFormsModule, StatusBadgeComponent],
    templateUrl: './risk-form.component.html',
    styleUrl: './risk-form.component.scss',
})
export class RiskFormComponent implements OnInit {
    private readonly riskService = inject(RiskService);
    private readonly fb = inject(FormBuilder);

    readonly projectId = input.required<number>();
    /** null adds a new risk. */
    readonly riskId = input<number | null>(null);

    readonly saved = output<Risk>();
    readonly cancelled = output<void>();

    protected readonly categoryLabels = RISK_CATEGORY_LABELS;
    protected readonly statusLabels = RISK_STATUS_LABELS;
    protected readonly severityLabels = RISK_SEVERITY_LABELS;
    protected readonly severityTones = RISK_SEVERITY_TONES;
    protected readonly categories = Object.keys(RISK_CATEGORY_LABELS) as RiskCategory[];
    protected readonly statuses = Object.keys(RISK_STATUS_LABELS) as RiskStatus[];
    protected readonly levels = [1, 2, 3, 4, 5];
    // A failed load leaves the list empty; the error interceptor has already told the user.
    protected readonly users = toSignal(inject(LookupService).users$.pipe(catchError(() => of([]))), { initialValue: [] });

    // Like the prototype: a new risk starts as Technical, 3 × 3, owned by the person adding it.
    protected readonly form = this.fb.nonNullable.group({
        title: ['', [Validators.required, Validators.maxLength(300)]],
        description: ['', Validators.maxLength(2000)],
        category: this.fb.nonNullable.control<RiskCategory>('Technical'),
        probability: this.fb.nonNullable.control(3),
        impact: this.fb.nonNullable.control(3),
        mitigation: ['', Validators.maxLength(2000)],
        ownerUserId: this.fb.control<number | null>(inject(AuthService).user()?.id ?? null),
        isSharedWithRequester: this.fb.nonNullable.control(false),
        status: this.fb.nonNullable.control<RiskStatus>('Open'),
    });

    private readonly levelsValue = toSignal(this.form.valueChanges, { initialValue: this.form.getRawValue() });

    /** Score and severity while choosing, so the person sees where the risk lands on the heat map. */
    protected readonly score = computed(() => {
        const value = this.levelsValue();
        return Number(value.probability ?? 3) * Number(value.impact ?? 3);
    });
    protected readonly severity = computed(() => severityOf(this.score()));

    /** The risk's code while editing ("RSK-003"). */
    protected readonly code = signal<string | null>(null);
    protected readonly loading = signal(false);
    protected readonly saving = signal(false);
    protected readonly submitted = signal(false);
    /** Messages from the API, by field name. */
    protected readonly serverErrors = signal<Record<string, string[]>>({});

    ngOnInit(): void {
        const id = this.riskId();
        if (id === null) {
            return;
        }

        this.loading.set(true);
        this.riskService.get(this.projectId(), id).subscribe({
            next: risk => {
                this.code.set(risk.code);
                this.form.setValue({
                    title: risk.title,
                    description: risk.description ?? '',
                    category: risk.category,
                    probability: risk.probability,
                    impact: risk.impact,
                    mitigation: risk.mitigation ?? '',
                    ownerUserId: risk.owner.id,
                    isSharedWithRequester: risk.isSharedWithRequester,
                    status: risk.status,
                });
                this.loading.set(false);
            },
            error: () => this.cancelled.emit(),
        });
    }

    /** The first message for a field: the API's first, then the form's own check after the first save. */
    protected errorFor(field: Field): string | null {
        const fromServer = this.serverErrors()[field];
        if (fromServer?.length) {
            return fromServer[0];
        }

        if (!this.submitted()) {
            return null;
        }

        const value = this.form.getRawValue();
        switch (field) {
            case 'title':
                if (!value.title.trim()) return 'Risk tanımı zorunludur.';
                if (value.title.length > 300) return 'Risk tanımı en fazla 300 karakter olabilir.';
                return null;
            case 'description':
                return value.description.length > 2000 ? 'Açıklama en fazla 2000 karakter olabilir.' : null;
            case 'mitigation':
                return value.mitigation.length > 2000 ? 'Önleyici aksiyon en fazla 2000 karakter olabilir.' : null;
            case 'ownerUserId':
                return value.ownerUserId === null ? 'Sorumlu seçilmelidir.' : null;
            default:
                return null;
        }
    }

    protected save(): void {
        this.submitted.set(true);
        this.serverErrors.set({});
        if (this.errorFor('title') || this.errorFor('description') || this.errorFor('mitigation')
            || this.errorFor('ownerUserId')) {
            return;
        }

        const value = this.form.getRawValue();
        const id = this.riskId();
        const body: SaveRisk = {
            title: value.title.trim(),
            description: value.description.trim() || null,
            category: value.category,
            probability: Number(value.probability),
            impact: Number(value.impact),
            mitigation: value.mitigation.trim() || null,
            ownerUserId: value.ownerUserId!,
            isSharedWithRequester: value.isSharedWithRequester,
            ...(id === null ? {} : { status: value.status }),
        };

        const request = id === null
            ? this.riskService.create(this.projectId(), body)
            : this.riskService.update(this.projectId(), id, body);

        this.saving.set(true);
        request.subscribe({
            next: risk => {
                this.saving.set(false);
                this.saved.emit(risk);
            },
            error: err => {
                this.saving.set(false);
                this.serverErrors.set(fieldErrorsOf(err));
            },
        });
    }
}
