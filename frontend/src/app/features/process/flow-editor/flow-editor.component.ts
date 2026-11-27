import { Component, computed, inject, signal } from '@angular/core';
import { rxResource, toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Observable, map } from 'rxjs';
import { fieldErrorsOf } from '../../../core/http/problem-details';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';
import { PERMISSION_LABELS } from '../../../shared/models/permission';
import { PROJECT_STATUS_LABELS } from '../../../shared/models/project-status';
import { DocumentService } from '../../documents/document.service';
import { CustomFieldService } from '../../projects/custom-fields/custom-field.service';
import {
    FlowOrder,
    PREREQUISITE_LABELS,
    applyOrder,
    isRuleOnInactiveField,
    moveStage,
    moveStep,
    orderOf,
    sameOrder,
} from '../models/flow-edit';
import {
    PROCESS_FLOW_USE_LABELS,
    ProcessFlow,
    ProcessFlowUse,
    ProcessStage,
    ProcessStep,
    StepPrerequisite,
} from '../models/process-stage';
import { ProcessService } from '../process.service';
import { StepFormComponent, StepFormTarget } from '../step-form/step-form.component';

/** The stage being added (id null) or renamed. */
interface StageDraft {
    id: number | null;
    title: string;
    shortTitle: string;
}

/**
 * /process-library/:code/edit (company administrator, Faz 7 · A8): the company's copy of a flow, edited without code.
 * Stages and steps are added, changed, switched off and put in order; the step form is StepFormComponent. Every change
 * is saved at once and the screen shows the flow the API returns (with its new rowVersion); the order is the exception:
 * it is moved with up / down and saved with "Sırayı kaydet". Contract: docs/configurability.md → Akış düzenleme.
 */
@Component({
    selector: 'app-flow-editor',
    imports: [FormsModule, RouterLink, EmptyStateComponent, StatusBadgeComponent, StepFormComponent],
    templateUrl: './flow-editor.component.html',
    styleUrl: './flow-editor.component.scss',
})
export class FlowEditorComponent {
    private readonly processService = inject(ProcessService);
    private readonly route = inject(ActivatedRoute);
    private readonly customFieldService = inject(CustomFieldService);
    private readonly documentService = inject(DocumentService);

    protected readonly permissionLabels = PERMISSION_LABELS;
    protected readonly statusLabels = PROJECT_STATUS_LABELS;
    protected readonly prerequisiteLabels = PREREQUISITE_LABELS;
    protected readonly useOptions = Object.entries(PROCESS_FLOW_USE_LABELS) as [ProcessFlowUse, string][];
    protected readonly useLabels = PROCESS_FLOW_USE_LABELS;

    private readonly code = toSignal(this.route.paramMap.pipe(map(p => p.get('code') ?? '')), { initialValue: '' });

    /** Loaded once; afterwards every save puts the flow the API returned here. */
    private readonly loaded = rxResource({
        params: () => this.code() || undefined,
        stream: ({ params }) => this.processService.getFlowForEditing(params),
    });
    private readonly saved = signal<ProcessFlow | null>(null);
    protected readonly flow = computed(() => this.saved() ?? (this.loaded.hasValue() ? this.loaded.value() : null));
    protected readonly loadFailed = computed(() => !!this.loaded.error() && !this.saved());

    // Inactive fields too, so a rule on a switched-off field can be pointed out.
    protected readonly customFields = rxResource({ stream: () => this.customFieldService.list(true) });
    protected readonly questionSets = rxResource({ stream: () => this.processService.questionSets() });
    protected readonly documentTypes = rxResource({ stream: () => this.documentService.types() });

    protected readonly showInactive = signal(false);
    protected readonly busy = signal(false);

    /** The order on the screen; differs from the saved flow until "Sırayı kaydet". */
    private readonly movedOrder = signal<FlowOrder | null>(null);
    protected readonly order = computed(() => this.movedOrder() ?? (this.flow() ? orderOf(this.flow()!) : null));
    protected readonly orderChanged = computed(() =>
        !!this.flow() && !!this.movedOrder() && !sameOrder(this.movedOrder()!, orderOf(this.flow()!)));

    protected readonly stages = computed<ProcessStage[]>(() => {
        const flow = this.flow();
        const order = this.order();
        if (!flow || !order) return [];
        const stages = applyOrder(flow, order);
        return this.showInactive()
            ? stages
            : stages.filter(s => s.isActive).map(s => ({ ...s, steps: s.steps.filter(step => step.isActive) }));
    });

    protected readonly inactiveCount = computed(() => {
        const flow = this.flow();
        if (!flow) return 0;
        return flow.stages.filter(s => !s.isActive).length
            + flow.stages.flatMap(s => s.steps).filter(s => !s.isActive).length;
    });

    // ---- Flow settings ----

    protected readonly settingsOpen = signal(false);
    protected flowName = '';
    protected flowUse: ProcessFlowUse | null = null;
    protected readonly settingsErrors = signal<Record<string, string[]>>({});

    protected openSettings(): void {
        const flow = this.flow()!;
        this.flowName = flow.name;
        this.flowUse = flow.usedFor;
        this.settingsErrors.set({});
        this.settingsOpen.set(true);
    }

    protected saveSettings(): void {
        const flow = this.flow()!;
        this.run(
            this.processService.updateFlow(flow.code, { name: this.flowName.trim(), usedFor: this.flowUse, rowVersion: flow.rowVersion }),
            () => this.settingsOpen.set(false),
            errors => this.settingsErrors.set(errors),
        );
    }

    // ---- Stages ----

    protected readonly stageDraft = signal<StageDraft | null>(null);
    protected readonly stageErrors = signal<Record<string, string[]>>({});

    protected editStage(stage: ProcessStage | null): void {
        this.stageErrors.set({});
        this.stageDraft.set(stage
            ? { id: stage.id, title: stage.title, shortTitle: stage.shortTitle }
            : { id: null, title: '', shortTitle: '' });
    }

    protected saveStage(): void {
        const draft = this.stageDraft()!;
        const flow = this.flow()!;
        const input = { title: draft.title.trim(), shortTitle: draft.shortTitle.trim(), rowVersion: flow.rowVersion };
        this.run(
            draft.id === null ? this.processService.addStage(flow.code, input) : this.processService.updateStage(draft.id, input),
            () => this.stageDraft.set(null),
            errors => this.stageErrors.set(errors),
        );
    }

    protected setStageActive(stage: ProcessStage, active: boolean): void {
        this.run(this.processService.setStageActive(stage.id, active, this.flow()!.rowVersion));
    }

    // ---- Steps ----

    protected readonly stepTarget = signal<StepFormTarget | null>(null);

    protected editStep(step: ProcessStep, stage: ProcessStage): void {
        this.stepTarget.set({ step, stageId: stage.id, afterStepId: null });
    }

    /** A new step at the end of the stage (after its last step, inactive ones included). */
    protected addStep(stage: ProcessStage): void {
        const all = this.flow()!.stages.find(s => s.id === stage.id)?.steps ?? [];
        this.stepTarget.set({ step: null, stageId: stage.id, afterStepId: all.at(-1)?.id ?? null });
    }

    protected onStepSaved(flow: ProcessFlow): void {
        this.accept(flow);
        this.stepTarget.set(null);
    }

    protected setStepActive(step: ProcessStep, active: boolean): void {
        this.run(this.processService.setStepActive(step.id, active, this.flow()!.rowVersion));
    }

    // ---- Order ----

    protected moveStage(stage: ProcessStage, delta: -1 | 1): void {
        this.movedOrder.set(this.moveVisible(o => moveStage(o, stage.id, delta)));
    }

    protected moveStep(step: ProcessStep, delta: -1 | 1): void {
        this.movedOrder.set(this.moveVisible(o => moveStep(o, step.id, delta)));
    }

    /** Whether the arrow would move anything on the screen (a step also moves into an empty stage next to it). */
    protected canMoveStage(stage: ProcessStage, delta: -1 | 1): boolean {
        return !sameOrder(this.moveVisible(o => moveStage(o, stage.id, delta)), this.order()!);
    }

    protected canMoveStep(step: ProcessStep, delta: -1 | 1): boolean {
        return !sameOrder(this.moveVisible(o => moveStep(o, step.id, delta)), this.order()!);
    }

    protected saveOrder(): void {
        const flow = this.flow()!;
        this.run(this.processService.reorderFlow(flow.code, { ...this.order()!, rowVersion: flow.rowVersion }));
    }

    protected cancelOrder(): void {
        this.movedOrder.set(null);
    }

    // ---- Showing a step ----

    protected prerequisiteText(p: StepPrerequisite): string {
        const label = this.prerequisiteLabels[p.kind];
        if (!p.value) return label;
        const names = p.kind === 'QuestionnaireComplete'
            ? (this.questionSets.hasValue() ? this.questionSets.value() : []).map(s => [s.code, s.name])
            : (this.documentTypes.hasValue() ? this.documentTypes.value() : []).map(d => [d.code, d.name]);
        return `${label}: ${names.find(([code]) => code === p.value)?.[1] ?? p.value}`;
    }

    protected hasRuleOnInactiveField(step: ProcessStep): boolean {
        const fields = this.customFields.hasValue() ? this.customFields.value() : [];
        return step.conditions.some(rule => isRuleOnInactiveField(rule, fields));
    }

    protected reload(): void {
        this.saved.set(null);
        this.movedOrder.set(null);
        this.loaded.reload();
    }

    // ---- Helpers ----

    /**
     * Moves once, and again while only hidden (inactive) items were passed, so a click always moves the item past one
     * that is on the screen.
     */
    private moveVisible(move: (o: FlowOrder) => FlowOrder): FlowOrder {
        // Stage and step ids come from different tables, so they are kept apart.
        const stages = new Set(this.stages().map(s => s.id));
        const steps = new Set(this.stages().flatMap(s => s.steps.map(x => x.id)));
        // The visible layout: stages and the steps in each, so a step changing stage counts as a move too.
        const visibleIds = (o: FlowOrder) => o.stages
            .filter(s => stages.has(s.id))
            .map(s => `${s.id}:${s.stepIds.filter(id => steps.has(id)).join(',')}`)
            .join('|');
        const start = this.order()!;
        let current = start;
        for (let i = 0; i < 200; i++) {
            const next = move(current);
            if (sameOrder(next, current)) break;
            current = next;
            if (visibleIds(current) !== visibleIds(start)) break;
        }
        return current;
    }

    /** The flow the API returned becomes the screen; an unsaved order is dropped (it was saved or is stale). */
    private accept(flow: ProcessFlow): void {
        this.saved.set(flow);
        this.movedOrder.set(null);
    }

    /**
     * Saves; field errors go to the form, anything else is shown by the error interceptor and the flow is loaded again
     * (409: someone else saved meanwhile, or the step still has records).
     */
    private run(
        request: Observable<ProcessFlow>,
        done?: () => void,
        fieldErrors?: (errors: Record<string, string[]>) => void,
    ): void {
        this.busy.set(true);
        request.subscribe({
            next: flow => {
                this.busy.set(false);
                this.accept(flow);
                done?.();
            },
            error: err => {
                this.busy.set(false);
                const errors = fieldErrorsOf(err);
                if (fieldErrors && Object.keys(errors).length > 0) {
                    fieldErrors(errors);
                } else {
                    this.reload();
                }
            },
        });
    }
}
