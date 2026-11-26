import { ProjectStatus } from '../../../shared/models/project-status';
import { Permission } from '../../../shared/models/permission';

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
    /** Who does the step: anyone with this permission (Faz 7). */
    responsiblePermission: Permission;
    /** Set on approval steps: only someone with this permission can close the step. */
    approverPermission: Permission | null;
    /** "No" closes the request with this status. */
    noOutcome: ProjectStatus | null;
    /** Steps the flow can return to when the answer is "No". */
    noJumps: ProcessStepJump[];
    /** When the step is done: on a record where all rules hold; empty, on every record (Faz 7 · A5). */
    conditions: StepConditionRule[];
    /** The rules as the screens show them, from the API ("Yalnızca dış talepte", "Seviye 2 ve üstü"). */
    conditionLabels: string[];
    /** Work that must exist on the project before the step is completed or answered "Yes" (Faz 7 · A4). */
    prerequisites: StepPrerequisite[];
}

export interface ProcessStepJump {
    targetStepId: number;
    targetStepCode: string;
    label: string;
}

/**
 * One skip rule of a step. `field`: "source", "requestType", "requiresOffer", "level" or "custom:{key}".
 * Contract: docs/configurability.md → Atlama kuralları.
 */
export interface StepConditionRule {
    field: string;
    op: 'equals' | 'notEquals' | 'atLeast' | 'atMost' | 'isSet' | 'isNotSet';
    value?: string | number | boolean | null;
    whenEmpty?: 'notMet' | 'met';
}

/** Whether any rule of the step looks at the field. */
export function dependsOn(step: ProcessStep, field: string): boolean {
    return step.conditions.some((rule) => rule.field === field);
}

export type PrerequisiteKind =
    | 'RequestFormComplete'
    | 'QuestionnaireComplete'
    | 'LevelApplied'
    | 'MeetingExists'
    | 'RiskExists'
    | 'RequirementsApproved'
    | 'DocumentApproved';

/** Rules: docs/configurability.md → Adım ön koşulları. `value` is a form or document type code where the kind needs one. */
export interface StepPrerequisite {
    kind: PrerequisiteKind;
    value?: string | null;
}
