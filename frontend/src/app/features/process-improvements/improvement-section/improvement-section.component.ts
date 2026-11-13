import { Component, OnInit, computed, inject, input, output, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { Observable } from 'rxjs';
import { fieldErrorsOf } from '../../../core/http/problem-details';
import { StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';
import {
    IMPROVEMENT_PRIORITY_LABELS,
    IMPROVEMENT_PRIORITY_TONES,
    Improvement,
    PILOT_RESULT_LABELS,
    PilotResult,
    priorityOf,
} from '../models/process-improvement';
import { ProcessImprovementService } from '../process-improvement.service';

export type ImprovementSection = 'prioritization' | 'study' | 'pilot';

/** A text field of a section: its key, label, limit and, for the bigger ones, a hint. */
interface TextField {
    key: string;
    label: string;
    limit: number;
    hint?: string;
    short?: boolean;
}

const STUDY_FIELDS: TextField[] = [
    { key: 'currentState', label: 'Mevcut durum', limit: 4000, hint: 'Süreç bugün nasıl işliyor? Süreci uygulayanlarla birlikte incelenir.' },
    { key: 'rootCause', label: 'Kök neden', limit: 4000, hint: 'Sorunun asıl nedeni (ör. 5 Neden, balık kılçığı).' },
    { key: 'solution', label: 'Çözüm', limit: 4000 },
    { key: 'pilotPlan', label: 'Pilot planı', limit: 4000, hint: 'Hangi proje veya ekipte, ne kadar süreyle denenecek?' },
    { key: 'pilotTarget', label: 'Pilot hedefi', limit: 1000, hint: 'Ölçülebilir olsun: "Talep kayıt süresi 2 günden 1 güne inecek".', short: true },
    { key: 'costBenefit', label: 'Fayda / maliyet ve etki analizi', limit: 4000, hint: 'Yönetim onayına (4. adım) sunulur.' },
];

const PILOT_FIELDS: TextField[] = [
    { key: 'pilotActual', label: 'Gerçekleşen', limit: 1000, hint: 'Pilot sonunda ölçülen değer.', short: true },
    { key: 'pilotFeedback', label: 'Pilot ekibinin geri bildirimi', limit: 4000 },
    { key: 'pilotDecision', label: 'Karar ve gerekçe', limit: 2000, short: true },
];

const SCORES: { key: 'impact' | 'frequency' | 'ease'; label: string; hint: string }[] = [
    { key: 'impact', label: 'Etki', hint: 'İyileşince ne kadar fayda sağlar?' },
    { key: 'frequency', label: 'Uygulama sıklığı', hint: 'Süreç ne sıklıkla işliyor?' },
    { key: 'ease', label: 'Uygulama kolaylığı', hint: 'Ne kadar kolay hayata geçer?' },
];

/**
 * Sections 2–4 of a proposal: prioritization, study (Çalışma Dokümanı) and pilot evaluation.
 * Each is saved on its own (the other sections keep their values). Read-only when the proposal is not active.
 */
@Component({
    selector: 'app-improvement-section',
    imports: [ReactiveFormsModule, StatusBadgeComponent],
    templateUrl: './improvement-section.component.html',
    styleUrl: './improvement-section.component.scss',
})
export class ImprovementSectionComponent implements OnInit {
    private readonly service = inject(ProcessImprovementService);
    private readonly fb = inject(FormBuilder);

    readonly improvement = input.required<Improvement>();
    readonly section = input.required<ImprovementSection>();
    readonly readOnly = input(false);

    readonly saved = output<Improvement>();

    protected readonly scores = SCORES;
    protected readonly scoreValues = [1, 2, 3, 4, 5];
    protected readonly priorityLabels = IMPROVEMENT_PRIORITY_LABELS;
    protected readonly priorityTones = IMPROVEMENT_PRIORITY_TONES;
    protected readonly pilotResultLabels = PILOT_RESULT_LABELS;
    protected readonly pilotResults = Object.keys(PILOT_RESULT_LABELS) as PilotResult[];

    protected readonly scoreForm = this.fb.group({
        impact: this.fb.control<number | null>(null),
        frequency: this.fb.control<number | null>(null),
        ease: this.fb.control<number | null>(null),
    });

    protected readonly textForm = this.fb.nonNullable.group(
        Object.fromEntries([...STUDY_FIELDS, ...PILOT_FIELDS].map(f => [f.key, ['']])) as Record<string, [string]>,
    );
    protected readonly pilotResult = this.fb.control<PilotResult | null>(null);

    protected readonly saving = signal(false);
    protected readonly savedAt = signal<Date | null>(null);
    protected readonly serverErrors = signal<Record<string, string[]>>({});
    protected readonly scoreError = signal<string | null>(null);

    private readonly scoreValue = toSignal(this.scoreForm.valueChanges, { initialValue: this.scoreForm.value });

    /** The score and priority as the person picks them, before saving. */
    protected readonly preview = computed(() => {
        const { impact, frequency, ease } = this.scoreValue();
        const score = impact && frequency && ease ? Number(impact) + Number(frequency) + Number(ease) : null;
        return { score, priority: priorityOf(score) };
    });

    protected readonly fields = computed(() => (this.section() === 'study' ? STUDY_FIELDS : PILOT_FIELDS));

    ngOnInit(): void {
        this.fill(this.improvement());
        if (this.readOnly()) {
            this.scoreForm.disable();
            this.textForm.disable();
            this.pilotResult.disable();
        }
    }

    private fill(i: Improvement): void {
        const { impact, frequency, ease } = i.prioritization;
        this.scoreForm.setValue({ impact, frequency, ease });
        const texts: Record<string, string | null> = { ...i.study, ...i.pilotEvaluation };
        this.textForm.setValue(Object.fromEntries(Object.keys(this.textForm.controls).map(k => [k, texts[k] ?? ''])));
        this.pilotResult.setValue(i.pilotEvaluation.pilotResult);
    }

    protected errorFor(field: TextField): string | null {
        const fromServer = this.serverErrors()[field.key];
        if (fromServer?.length) return fromServer[0];
        const value = this.textForm.controls[field.key].value;
        return value.length > field.limit ? `${field.label} en fazla ${field.limit} karakter olabilir.` : null;
    }

    protected clearScores(): void {
        this.scoreForm.setValue({ impact: null, frequency: null, ease: null });
    }

    protected save(): void {
        this.serverErrors.set({});
        this.scoreError.set(null);
        const id = this.improvement().id;
        const text = (key: string) => this.textForm.controls[key].value.trim() || null;

        let request: Observable<Improvement>;
        switch (this.section()) {
            case 'prioritization': {
                const v = this.scoreForm.getRawValue();
                const given = [v.impact, v.frequency, v.ease].filter(s => s !== null).length;
                if (given !== 0 && given !== 3) {
                    this.scoreError.set('Etki, uygulama sıklığı ve kolaylık birlikte girilmelidir.');
                    return;
                }
                request = this.service.savePrioritization(id, { impact: v.impact, frequency: v.frequency, ease: v.ease });
                break;
            }
            case 'study':
                if (STUDY_FIELDS.some(f => this.errorFor(f))) return;
                request = this.service.saveStudy(id, {
                    currentState: text('currentState'), rootCause: text('rootCause'), solution: text('solution'),
                    pilotPlan: text('pilotPlan'), pilotTarget: text('pilotTarget'), costBenefit: text('costBenefit'),
                });
                break;
            default:
                if (PILOT_FIELDS.some(f => this.errorFor(f))) return;
                request = this.service.savePilotEvaluation(id, {
                    pilotActual: text('pilotActual'), pilotFeedback: text('pilotFeedback'),
                    pilotResult: this.pilotResult.value, pilotDecision: text('pilotDecision'),
                });
        }

        this.saving.set(true);
        request.subscribe({
            next: improvement => {
                this.saving.set(false);
                this.savedAt.set(new Date());
                this.fill(improvement);
                this.saved.emit(improvement);
            },
            error: err => {
                this.saving.set(false);
                this.serverErrors.set(fieldErrorsOf(err));
            },
        });
    }
}
