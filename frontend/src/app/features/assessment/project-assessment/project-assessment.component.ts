import { Component, computed, inject, input, output, signal } from '@angular/core';
import { StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';
import { QuestionnaireComponent } from '../../../shared/question-bank/questionnaire/questionnaire.component';
import { Questionnaire } from '../../../shared/question-bank/question-bank.models';
import { AssessmentService } from '../assessment.service';
import {
    CUSTOMER_FIT_DESCRIPTIONS, CUSTOMER_FIT_LABELS, CUSTOMER_FIT_SET, CUSTOMER_FIT_TONES, CustomerFitResult, LEVEL_SECTION_TITLES,
    LEVEL_SET, LevelResult, canApplyLevel, differsFromApplied,
} from '../models/assessment';

/**
 * Seviye & Uygunluk (Faz 4 · B2): the customer fit survey (step 4, external requests only) and the level form (step 5),
 * each with its result card. The forms are the shared app-questionnaire; every click saves at once.
 * Takes the project id, so the menu page and the project detail page can both show it. Contract: docs/assessment-api.md
 */
@Component({
    selector: 'app-project-assessment',
    imports: [QuestionnaireComponent, StatusBadgeComponent],
    templateUrl: './project-assessment.component.html',
    styleUrl: './project-assessment.component.scss',
})
export class ProjectAssessmentComponent {
    private readonly assessmentService = inject(AssessmentService);

    readonly projectId = input.required<number>();

    /** True when the project is not active: the forms can be read but not changed. */
    readonly readOnly = input(false);

    /** After "Seviyeyi projeye uygula", so the project page can show the new level. */
    readonly levelChanged = output<void>();

    protected readonly levelSet = LEVEL_SET;
    protected readonly fitSet = CUSTOMER_FIT_SET;
    protected readonly sectionTitles = LEVEL_SECTION_TITLES;
    protected readonly fitLabels = CUSTOMER_FIT_LABELS;
    protected readonly fitTones = CUSTOMER_FIT_TONES;
    protected readonly fitDescriptions = CUSTOMER_FIT_DESCRIPTIONS;

    protected readonly level = signal<LevelResult | null>(null);
    protected readonly fit = signal<CustomerFitResult | null>(null);
    protected readonly applying = signal(false);

    /** Internal and affiliated requests list no survey questions: the survey is not filled in. */
    protected readonly fitListed = computed(() => (this.fit()?.listed ?? 0) > 0);

    protected readonly canApply = computed(() => {
        const result = this.level();
        return !this.readOnly() && !!result && canApplyLevel(result) && differsFromApplied(result);
    });

    protected readonly differs = computed(() => {
        const result = this.level();
        return !!result && result.appliedLevel !== null && differsFromApplied(result);
    });

    protected onLevelChanged(form: Questionnaire): void {
        this.level.set(form.result as LevelResult);
    }

    protected onFitChanged(form: Questionnaire): void {
        this.fit.set(form.result as CustomerFitResult);
    }

    protected applyLevel(): void {
        this.applying.set(true);
        this.assessmentService.applyLevel(this.projectId()).subscribe({
            next: form => {
                this.level.set(form.result as LevelResult);
                this.applying.set(false);
                this.levelChanged.emit();
            },
            // The error interceptor shows the message.
            error: () => this.applying.set(false),
        });
    }
}
