import { Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { map } from 'rxjs';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';
import { PROJECT_STATUS_LABELS } from '../../../shared/models/project-status';
import { USER_ROLE_LABELS } from '../../../shared/models/user-role';
import { ProcessStage } from '../models/process-stage';
import { ProcessService } from '../process.service';

/**
 * Süreç Kütüphanesi: the 13 stages of the ISDLC flow and what happens at each step (read only).
 * The selected stage is kept in the URL (?stage=4) so a link can open it directly.
 */
@Component({
    selector: 'app-process-library',
    imports: [EmptyStateComponent, StatusBadgeComponent],
    templateUrl: './process-library.component.html',
    styleUrl: './process-library.component.scss',
})
export class ProcessLibraryComponent {
    private readonly processService = inject(ProcessService);
    private readonly route = inject(ActivatedRoute);
    private readonly router = inject(Router);

    protected readonly roleLabels = USER_ROLE_LABELS;
    protected readonly statusLabels = PROJECT_STATUS_LABELS;

    /** undefined while loading. */
    protected readonly stages = signal<ProcessStage[] | undefined>(undefined);
    protected readonly loadFailed = signal(false);

    private readonly stageParam = toSignal(
        this.route.queryParamMap.pipe(map(params => Number(params.get('stage')))),
        { initialValue: NaN },
    );

    /** The stage in the URL, or the first stage when the URL has none (or an unknown one). */
    protected readonly selected = computed(() => {
        const stages = this.stages() ?? [];
        return stages.find(s => s.order === this.stageParam()) ?? stages[0];
    });

    protected readonly summary = computed(() => {
        const steps = (this.stages() ?? []).flatMap(s => s.steps);
        return {
            stageCount: this.stages()?.length ?? 0,
            stepCount: steps.length,
            decisionCount: steps.filter(s => s.isDecision).length,
            approvalCount: steps.filter(s => s.approverRole).length,
        };
    });

    constructor() {
        // The error interceptor already tells the user what went wrong; the page only shows it is empty.
        this.processService.getStages().subscribe({
            next: stages => this.stages.set(stages),
            error: () => this.loadFailed.set(true),
        });
    }

    protected select(stage: ProcessStage): void {
        this.router.navigate([], { relativeTo: this.route, queryParams: { stage: stage.order }, replaceUrl: true });
    }
}
