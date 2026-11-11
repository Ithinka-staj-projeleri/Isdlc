import { ProjectStatus } from '../../../shared/models/project-status';
import { UserRole } from '../../../shared/models/user-role';

/** Which menu lists the flow's records. */
export type ProcessFlowKind = 'Request' | 'Improvement' | 'ResourcePlanning' | 'Billing';

/** GET /api/v1/process-flows — a flow without its steps. */
export interface ProcessFlowListItem {
    id: number;
    code: string;
    name: string;
    kind: ProcessFlowKind;
    /** The stage from which a record counts as a project; null: never. */
    projectStageOrder: number | null;
    stepCount: number;
}

/** GET /api/v1/process-flows/{code} — a flow with its stages and steps in flow order. */
export interface ProcessFlow {
    id: number;
    code: string;
    name: string;
    kind: ProcessFlowKind;
    projectStageOrder: number | null;
    stages: ProcessStage[];
}

/** A stage ("bölüm") of a flow with its steps in flow order. */
export interface ProcessStage {
    id: number;
    order: number;
    title: string;
    shortTitle: string;
    steps: ProcessStep[];
}

export interface ProcessStep {
    id: number;
    code: string;
    order: number;
    text: string;
    isDecision: boolean;
    /** What the "No" answer means, in the prototype's words. */
    noBranchText: string | null;
    responsibleRole: UserRole;
    /** Set on approval steps: only this role can act on the step. */
    approverRole: UserRole | null;
    /** "No" closes the request with this status. */
    noOutcome: ProjectStatus | null;
    /** Steps the flow can return to when the answer is "No". */
    noJumps: ProcessStepJump[];
    /** When the step is done; all false / null: on every record. */
    condition: StepCondition;
}

export interface ProcessStepJump {
    targetStepId: number;
    targetStepCode: string;
    label: string;
}

/** Contract: docs/process-api.md → "Adım koşulları". */
export interface StepCondition {
    requiresExternalRequest: boolean;
    requiresOffer: boolean;
    minLevel: number | null;
}

/** Short Turkish labels of a step's conditions, e.g. ["Yalnızca dış talepte"]; empty when the step is always done. */
export function conditionLabels(condition: StepCondition): string[] {
    const labels: string[] = [];
    if (condition.requiresExternalRequest) labels.push('Yalnızca dış talepte');
    if (condition.requiresOffer) labels.push('Teklif hazırlanacaksa');
    if (condition.minLevel !== null) labels.push(`Seviye ${condition.minLevel} ve üstü`);
    return labels;
}
