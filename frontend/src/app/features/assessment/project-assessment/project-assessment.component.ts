import { Component, computed, inject, input, output, signal } from '@angular/core';
import { rxResource, toSignal } from '@angular/core/rxjs-interop';
import { Observable, catchError, of } from 'rxjs';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';
import { LocalDatePipe } from '../../../shared/pipes/local-date-pipe';
import { AssessmentService } from '../assessment.service';
import { RECOMMENDATION_LABELS, RECOMMENDATION_TONES, ProjectAssessment } from '../models/assessment';

/**
 * Level questions (flow 2.4) and acceptance scoring (flow 3.1) of a project.
 * A click saves at once, like the paper form being filled in; clicking the chosen option again clears it.
 * Takes the project id, so the menu page and the project detail page can both show it.
 */
@Component({
    selector: 'app-project-assessment',
    imports: [EmptyStateComponent, StatusBadgeComponent, LocalDatePipe],
    templateUrl: './project-assessment.component.html',
    styleUrl: './project-assessment.component.scss',
})
export class ProjectAssessmentComponent {
    private readonly assessmentService = inject(AssessmentService);

    readonly projectId = input.required<number>();

    /** True when the project is not active: the assessment can be read but not changed. */
    readonly readOnly = input(false);

    /** After "Seviyeyi uygula", so the project page can show the new level. */
    readonly levelChanged = output<void>();

    protected readonly recommendationLabels = RECOMMENDATION_LABELS;
    protected readonly recommendationTones = RECOMMENDATION_TONES;
    protected readonly acceptanceLevels = [1, 2, 3, 4, 5];

    // A failed load shows a message; the error interceptor has already told the user why.
    protected readonly definition = toSignal(
        this.assessmentService.definition$.pipe(catchError(() => of(null))), { initialValue: undefined });

    protected readonly assessment = rxResource({
        params: () => this.projectId(),
        stream: ({ params }) => this.assessmentService.get(params),
    });

    /** True while a click is being saved, so a second click cannot overtake the first. */
    protected readonly saving = signal(false);

    protected readonly levelQuestionCount = computed(() => this.definition()?.levelQuestions.length ?? 0);
    protected readonly criterionCount = computed(() => this.definition()?.acceptanceCriteria.length ?? 0);

    /** The suggested level can be applied: every question answered and it differs from the applied one. */
    protected readonly canApply = computed(() => {
        const level = this.assessment.value()?.level;
        return !this.readOnly() && !!level?.suggestedLevel && level.suggestedLevel !== level.appliedLevel;
    });

    protected answerLevel(key: string, value: number): void {
        const current = this.assessment.value();
        if (!current) return;
        const answers = { ...current.level.answers, [key]: current.level.answers[key] === value ? null : value };
        this.save(this.assessmentService.saveLevelAnswers(this.projectId(), answers));
    }

    protected scoreCriterion(key: string, value: number): void {
        const current = this.assessment.value();
        if (!current) return;
        const scores = { ...current.acceptance.scores, [key]: current.acceptance.scores[key] === value ? null : value };
        this.save(this.assessmentService.saveAcceptanceScores(this.projectId(), scores));
    }

    protected applyLevel(): void {
        this.save(this.assessmentService.applyLevel(this.projectId()), () => this.levelChanged.emit());
    }

    /** 2 decimals with a comma, like the paper form: 3,70. */
    protected formatScore(value: number): string {
        return value.toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    }

    private save(request: Observable<ProjectAssessment>, afterSave?: () => void): void {
        this.saving.set(true);
        request.subscribe({
            next: result => {
                this.assessment.set(result);
                this.saving.set(false);
                afterSave?.();
            },
            // The error interceptor shows the message; reload so the screen shows what is really saved.
            error: () => {
                this.saving.set(false);
                this.assessment.reload();
            },
        });
    }
}
