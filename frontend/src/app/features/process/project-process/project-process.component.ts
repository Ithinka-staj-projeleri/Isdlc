import { Component, computed, inject, input, output, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { catchError, of } from 'rxjs';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { BadgeTone, StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';
import { PROJECT_STATUS_LABELS } from '../../../shared/models/project-status';
import { USER_ROLE_LABELS } from '../../../shared/models/user-role';
import { LocalDatePipe } from '../../../shared/pipes/local-date-pipe';
import { ProcessStage, ProcessStep, conditionLabels } from '../models/process-stage';
import { ProcessStageState, ProjectProcess, STEP_ACTION_LABELS, StepRecord } from '../models/project-process';
import { ProcessService } from '../process.service';
import { StepActionsComponent } from '../step-actions/step-actions.component';

type StepState = 'done' | 'current' | 'stopped' | 'pending' | 'skipped';

/** Records loaded for the history and the answer badges; enough for the 75-step flow with some returns. */
const RECORD_LIMIT = 100;
const HISTORY_PAGE = 10;

const STAGE_STATE_LABELS: Record<ProcessStageState, string> = {
    Completed: 'Tamamlandı',
    Current: 'Devam ediyor',
    Pending: 'Bekliyor',
    Stopped: 'Durduruldu',
};

/**
 * Süreç tab of the project detail page: where the project stands in its flow, the actions the
 * signed-in user can take on the current step, and the process history.
 * What the user may do comes from the API (availableActions); the screen only follows it.
 */
@Component({
    selector: 'app-project-process',
    imports: [EmptyStateComponent, StatusBadgeComponent, LocalDatePipe, StepActionsComponent],
    templateUrl: './project-process.component.html',
    styleUrl: './project-process.component.scss',
})
export class ProjectProcessComponent {
    private readonly processService = inject(ProcessService);

    readonly projectId = input.required<number>();

    /**
     * After a step command (complete, answer, reject), so the page can refresh what it shows about the project
     * (current step, status): (processChanged)="project.reload()". Also after a failed command, since a 409
     * means someone else moved the project on meanwhile.
     */
    readonly processChanged = output<void>();

    protected readonly roleLabels = USER_ROLE_LABELS;
    protected readonly statusLabels = PROJECT_STATUS_LABELS;
    protected readonly actionLabels = STEP_ACTION_LABELS;
    protected readonly stageStateLabels = STAGE_STATE_LABELS;

    protected readonly process = rxResource({
        params: () => ({ projectId: this.projectId() }),
        stream: ({ params }) => this.processService.getProjectProcess(params.projectId),
    });

    /** The stages and steps of the project's own flow; empty until the project's process is loaded. */
    private readonly flowResource = rxResource({
        params: () => (this.process.hasValue() ? { code: this.process.value().flow.code } : undefined),
        stream: ({ params }) =>
            this.processService.getFlow(params.code).pipe(catchError(() => of({ stages: [] as ProcessStage[] }))),
    });

    protected readonly flow = computed<ProcessStage[]>(() =>
        this.flowResource.hasValue() ? this.flowResource.value().stages : []);

    private readonly skippedStepIds = computed(() =>
        new Set(this.process.hasValue() ? this.process.value().skippedStepIds : []));

    protected readonly conditionLabels = conditionLabels;

    protected readonly records = rxResource({
        params: () => ({ projectId: this.projectId() }),
        stream: ({ params }) => this.processService.getStepRecords(params.projectId, 1, RECORD_LIMIT),
    });

    /** The stage the user clicked; null follows the project (current or stopped stage). */
    private readonly pickedStage = signal<number | null>(null);
    protected readonly historyShown = signal(HISTORY_PAGE);

    private readonly stepsById = computed(() => {
        const map = new Map<number, ProcessStep>();
        for (const stage of this.flow()) for (const step of stage.steps) map.set(step.id, step);
        return map;
    });

    private readonly recordItems = computed<StepRecord[]>(() => (this.records.hasValue() ? this.records.value().items : []));

    /** The newest record of each step, for the "Cevap: Evet" style badges. */
    protected readonly latestRecordByStep = computed(() => {
        const map = new Map<number, StepRecord>();
        for (const record of this.recordItems()) if (!map.has(record.stepId)) map.set(record.stepId, record);
        return map;
    });

    /**
     * How far the project got, as a step order. Active: the current step. Finished: past the last step.
     * Stopped (rejected, lost, redirected): the step of the last record, where the request was closed.
     */
    private readonly reachedOrder = computed(() => {
        const process = this.process.hasValue() ? this.process.value() : undefined;
        if (!process) return 0;
        if (process.currentStep) return process.currentStep.order;
        if (process.status === 'Closed') return Number.MAX_SAFE_INTEGER;
        const last = this.recordItems()[0];
        return (last && this.stepsById().get(last.stepId)?.order) ?? 0;
    });

    protected readonly selectedStage = computed<ProcessStage | undefined>(() => {
        const flow = this.flow();
        const picked = this.pickedStage();
        if (picked !== null) return flow.find(s => s.order === picked);

        const stages = this.process.hasValue() ? this.process.value().stages : [];
        const followed = stages.find(s => s.state === 'Current') ?? stages.find(s => s.state === 'Stopped');
        return flow.find(s => s.order === followed?.order) ?? flow[flow.length - 1];
    });

    protected readonly history = computed(() => this.recordItems().slice(0, this.historyShown()));

    protected stepState(step: ProcessStep, process: ProjectProcess): StepState {
        if (this.skippedStepIds().has(step.id)) return 'skipped';
        const reached = this.reachedOrder();
        if (step.order < reached) return 'done';
        if (step.order > reached) return 'pending';
        return process.currentStep ? 'current' : 'stopped';
    }

    protected stageTone(state: ProcessStageState): BadgeTone {
        return state === 'Completed' ? 'ok' : state === 'Current' ? 'info' : state === 'Stopped' ? 'bad' : 'neutral';
    }

    protected statusTone(process: ProjectProcess): BadgeTone {
        return process.status === 'Active' ? 'info' : process.status === 'Closed' ? 'ok' : 'bad';
    }

    /** Badge for what was last done on a step. */
    protected recordBadge(record: StepRecord): { label: string; tone: BadgeTone } {
        switch (record.action) {
            case 'Yes':
                return { label: 'Cevap: Evet', tone: 'ok' };
            case 'No':
                return { label: 'Cevap: Hayır' + (record.jumpedToStepCode ? ` → ${record.jumpedToStepCode}` : ''), tone: 'bad' };
            case 'Rejected':
                return { label: 'Reddedildi', tone: 'bad' };
            default:
                return { label: 'Tamamlandı', tone: 'ok' };
        }
    }

    protected pickStage(order: number): void {
        this.pickedStage.set(order);
    }

    /** A command succeeded: show the new state, follow the project to its new step, refresh the history. */
    protected onUpdated(process: ProjectProcess): void {
        this.process.set(process);
        this.pickedStage.set(null);
        this.records.reload();
        this.processChanged.emit();
    }

    /** A command failed (e.g. someone else moved the project on): load the current state again. */
    protected onFailed(): void {
        this.process.reload();
        this.records.reload();
        this.processChanged.emit();
    }
}
