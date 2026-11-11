import { Component, computed, inject } from '@angular/core';
import { rxResource, toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { map } from 'rxjs';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';
import { PROJECT_STATUS_LABELS } from '../../../shared/models/project-status';
import { USER_ROLE_LABELS } from '../../../shared/models/user-role';
import { ProcessFlowListItem, ProcessStage, conditionLabels } from '../models/process-stage';
import { ProcessService } from '../process.service';

/**
 * Süreç Kütüphanesi: the company's flows, their stages and what happens at each step (read only).
 * The selected flow and stage are kept in the URL (?flow=new-software-project&stage=4) so a link can open them directly.
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
    protected readonly conditionLabels = conditionLabels;

    // The error interceptor already tells the user what went wrong; the page only shows it is empty.
    protected readonly flows = rxResource({ stream: () => this.processService.flows$ });

    private readonly params = toSignal(
        this.route.queryParamMap.pipe(map(params => ({ flow: params.get('flow'), stage: Number(params.get('stage')) }))),
        { initialValue: { flow: null as string | null, stage: NaN } },
    );

    /** The flow in the URL, or the first flow when the URL has none (or an unknown one). */
    protected readonly selectedFlow = computed<ProcessFlowListItem | undefined>(() => {
        const flows = this.flows.hasValue() ? this.flows.value() : [];
        return flows.find(f => f.code === this.params().flow) ?? flows[0];
    });

    protected readonly flow = rxResource({
        params: () => (this.selectedFlow() ? { code: this.selectedFlow()!.code } : undefined),
        stream: ({ params }) => this.processService.getFlow(params.code),
    });

    protected readonly stages = computed<ProcessStage[]>(() => (this.flow.hasValue() ? this.flow.value().stages : []));

    /** The stage in the URL, or the first stage when the URL has none (or an unknown one). */
    protected readonly selected = computed(() => {
        const stages = this.stages();
        return stages.find(s => s.order === this.params().stage) ?? stages[0];
    });

    protected readonly summary = computed(() => {
        const steps = this.stages().flatMap(s => s.steps);
        return {
            stageCount: this.stages().length,
            stepCount: steps.length,
            decisionCount: steps.filter(s => s.isDecision).length,
            approvalCount: steps.filter(s => s.approverRole).length,
        };
    });

    protected selectFlow(flow: ProcessFlowListItem): void {
        // A new flow starts at its first stage.
        this.router.navigate([], { relativeTo: this.route, queryParams: { flow: flow.code, stage: null }, replaceUrl: true });
    }

    protected select(stage: ProcessStage): void {
        this.router.navigate([], {
            relativeTo: this.route,
            queryParams: { stage: stage.order },
            queryParamsHandling: 'merge',
            replaceUrl: true,
        });
    }
}
