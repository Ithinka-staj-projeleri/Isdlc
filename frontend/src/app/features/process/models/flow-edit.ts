import { Permission } from '../../../shared/models/permission';
import { ProjectStatus } from '../../../shared/models/project-status';
import { CustomField } from '../../projects/custom-fields/custom-field';
import { REQUEST_SOURCE_LABELS, REQUEST_TYPE_LABELS } from '../../projects/project-labels';
import {
    ConditionOperator,
    PrerequisiteKind,
    ProcessFlow,
    ProcessFlowUse,
    ProcessStage,
    StepConditionRule,
    StepPrerequisite,
} from './process-stage';

// Flow editing (Faz 7 · A7 backend, A8 screen). Every write sends the flow's rowVersion and returns the whole flow.
// Contract: docs/configurability.md → Akış düzenleme.

export interface UpdateFlowInput {
    name: string;
    usedFor: ProcessFlowUse | null;
    rowVersion: string;
}

export interface SaveStageInput {
    title: string;
    shortTitle: string;
    rowVersion: string;
}

export interface StepJumpInput {
    targetStepId: number | null;
    label: string;
}

/** PUT /process-steps/{id}; POST /process-flows/{code}/steps adds stageId and afterStepId. */
export interface SaveStepInput {
    text: string;
    isDecision: boolean;
    noBranchText: string | null;
    responsiblePermission: Permission | null;
    approverPermission: Permission | null;
    noOutcome: ProjectStatus | null;
    noJumps: StepJumpInput[];
    conditions: StepConditionRule[];
    prerequisites: StepPrerequisite[];
    rowVersion: string;
}

export interface AddStepInput extends SaveStepInput {
    stageId: number;
    /** The step it comes after; null: first in the stage. */
    afterStepId: number | null;
}

export interface ReorderInput {
    stages: { id: number; stepIds: number[] }[];
    rowVersion: string;
}

/** What "Hayır" may do to the record when it closes it. */
export const NO_OUTCOMES: readonly ProjectStatus[] = ['Rejected', 'Lost', 'Redirected'];

export const MAX_RULES = 10;
export const MAX_PREREQUISITES = 10;

// ---- Skip rules ----

export type ConditionFieldType = 'Choice' | 'Boolean' | 'Number' | 'Date' | 'Text';

/** A field a rule can look at, as the backend's StepRuleValidation knows it. */
export interface ConditionField {
    field: string;
    name: string;
    type: ConditionFieldType;
    /** Allowed values of a choice: [value, label]. */
    options?: [string, string][];
}

/** The record's own fields; the same for every company (StepRuleValidation.BuiltInFields). */
export const BUILT_IN_FIELDS: readonly ConditionField[] = [
    { field: 'source', name: 'Talep kaynağı', type: 'Choice', options: Object.entries(REQUEST_SOURCE_LABELS) },
    { field: 'requestType', name: 'Talep türü', type: 'Choice', options: Object.entries(REQUEST_TYPE_LABELS) },
    { field: 'requiresOffer', name: 'Teklif hazırlanacak mı', type: 'Boolean' },
    { field: 'level', name: 'Seviye', type: 'Number' },
];

export const CUSTOM_PREFIX = 'custom:';

/** The record's fields and the company's active custom fields, for the rule's field list. */
export function conditionFields(customFields: readonly CustomField[]): ConditionField[] {
    const custom = customFields
        .filter((f) => f.isActive)
        .map<ConditionField>((f) => ({
            field: CUSTOM_PREFIX + f.key,
            name: f.label,
            type: f.type === 'Select' ? 'Choice' : f.type,
            options: f.type === 'Select' ? f.options.map((o) => [o, o] as [string, string]) : undefined,
        }));
    return [...BUILT_IN_FIELDS, ...custom];
}

export const OPERATOR_LABELS: Record<ConditionOperator, string> = {
    Equals: 'eşittir',
    NotEquals: 'eşit değildir',
    AtLeast: 'en az',
    AtMost: 'en çok',
    IsSet: 'dolu',
    IsNotSet: 'boş',
};

/** "En az / en çok" only for numbers and dates (same as the API). */
export function operatorsFor(type: ConditionFieldType | undefined): ConditionOperator[] {
    const all: ConditionOperator[] = ['Equals', 'NotEquals', 'IsSet', 'IsNotSet'];
    return type === 'Number' || type === 'Date' ? ['Equals', 'NotEquals', 'AtLeast', 'AtMost', 'IsSet', 'IsNotSet'] : all;
}

export function operatorTakesValue(op: ConditionOperator): boolean {
    return op !== 'IsSet' && op !== 'IsNotSet';
}

/**
 * A rule on a custom field the company switched off (or never had): it never holds, so the flow screen warns about it
 * (docs/configurability.md → Atlama kuralları).
 */
export function isRuleOnInactiveField(rule: StepConditionRule, customFields: readonly CustomField[]): boolean {
    if (!rule.field.startsWith(CUSTOM_PREFIX)) return false;
    const key = rule.field.slice(CUSTOM_PREFIX.length);
    return !customFields.some((f) => f.key === key && f.isActive);
}

/** The value as the API takes it: true / false for yes-no, a number for numbers, text otherwise; null without one. */
export function ruleValue(type: ConditionFieldType | undefined, raw: unknown): string | number | boolean | null {
    if (raw === null || raw === undefined || raw === '') return null;
    switch (type) {
        case 'Boolean':
            return raw === true || raw === 'true' ? true : raw === false || raw === 'false' ? false : null;
        case 'Number': {
            const n = typeof raw === 'number' ? raw : Number(String(raw).replace(',', '.'));
            return Number.isFinite(n) ? n : null;
        }
        default:
            return String(raw).trim() || null;
    }
}

// ---- Prerequisites ----

export const PREREQUISITE_LABELS: Record<PrerequisiteKind, string> = {
    RequestFormComplete: 'Talep Kayıt Formu dolu',
    QuestionnaireComplete: 'Form tamamlandı',
    LevelApplied: 'Seviye uygulandı',
    MeetingExists: 'En az bir toplantı tutanağı var',
    RiskExists: 'En az bir risk var',
    RequirementsApproved: 'Gereksinimler onaylandı',
    DocumentApproved: 'Doküman onaylandı',
};

export const PREREQUISITE_KINDS = Object.keys(PREREQUISITE_LABELS) as PrerequisiteKind[];

/** Kinds that name a form or a document type. */
export function prerequisiteTakesValue(kind: PrerequisiteKind): boolean {
    return kind === 'QuestionnaireComplete' || kind === 'DocumentApproved';
}

// ---- Order ----

/** The flow's stages and steps as ids, for "Sırayı kaydet". */
export interface FlowOrder {
    stages: { id: number; stepIds: number[] }[];
}

export function orderOf(flow: Pick<ProcessFlow, 'stages'>): FlowOrder {
    return { stages: flow.stages.map((s) => ({ id: s.id, stepIds: s.steps.map((step) => step.id) })) };
}

export function sameOrder(a: FlowOrder, b: FlowOrder): boolean {
    return JSON.stringify(a) === JSON.stringify(b);
}

/** A stage one place up (-1) or down (+1). Nothing happens at the ends. */
export function moveStage(order: FlowOrder, stageId: number, delta: -1 | 1): FlowOrder {
    const stages = [...order.stages];
    const from = stages.findIndex((s) => s.id === stageId);
    const to = from + delta;
    if (from < 0 || to < 0 || to >= stages.length) return order;
    [stages[from], stages[to]] = [stages[to], stages[from]];
    return { stages };
}

/**
 * A step one place up or down in flow order: inside its stage, or past the stage's edge into the stage before
 * (as its last step) or after (as its first). Nothing happens at the flow's ends.
 */
export function moveStep(order: FlowOrder, stepId: number, delta: -1 | 1): FlowOrder {
    const stages = order.stages.map((s) => ({ id: s.id, stepIds: [...s.stepIds] }));
    const si = stages.findIndex((s) => s.stepIds.includes(stepId));
    if (si < 0) return order;
    const steps = stages[si].stepIds;
    const i = steps.indexOf(stepId);
    const j = i + delta;
    if (j >= 0 && j < steps.length) {
        [steps[i], steps[j]] = [steps[j], steps[i]];
        return { stages };
    }
    const next = si + delta;
    if (next < 0 || next >= stages.length) return order;
    steps.splice(i, 1);
    if (delta < 0) stages[next].stepIds.push(stepId);
    else stages[next].stepIds.unshift(stepId);
    return { stages };
}

/** The stages laid out in the given order, for showing an unsaved order. */
export function applyOrder(flow: ProcessFlow, order: FlowOrder): ProcessStage[] {
    const stageById = new Map(flow.stages.map((s) => [s.id, s]));
    const stepById = new Map(flow.stages.flatMap((s) => s.steps).map((step) => [step.id, step]));
    return order.stages
        .filter((o) => stageById.has(o.id))
        .map((o) => ({
            ...stageById.get(o.id)!,
            steps: o.stepIds.filter((id) => stepById.has(id)).map((id) => stepById.get(id)!),
        }));
}
