import { BadgeTone } from '../../../shared/components/status-badge/status-badge.component';
import { ProjectStatus } from '../../../shared/models/project-status';
import { UserSummary } from '../../../shared/models/user-summary';

// Contract: docs/process-improvement-api.md

export type ImprovementSource = 'Retrospective' | 'DefectOrComplaint' | 'EmployeeSuggestion' | 'ManagementGoal';
export type ImprovementPriority = 'High' | 'Medium' | 'Low';
export type PilotResult = 'Met' | 'PartiallyMet' | 'NotMet';

export const IMPROVEMENT_SOURCE_LABELS: Record<ImprovementSource, string> = {
    Retrospective: 'Proje kapanışı / retrospektif',
    DefectOrComplaint: 'Hata, sapma, müşteri şikâyeti',
    EmployeeSuggestion: 'Çalışan önerisi',
    ManagementGoal: 'Yönetim hedefi',
};

export const IMPROVEMENT_PRIORITY_LABELS: Record<ImprovementPriority, string> = {
    High: 'Yüksek',
    Medium: 'Orta',
    Low: 'Düşük',
};

export const IMPROVEMENT_PRIORITY_TONES: Record<ImprovementPriority, BadgeTone> = {
    High: 'bad',
    Medium: 'warn',
    Low: 'neutral',
};

export const PILOT_RESULT_LABELS: Record<PilotResult, string> = {
    Met: 'Karşılandı',
    PartiallyMet: 'Kısmen karşılandı',
    NotMet: 'Karşılanmadı',
};

/** The flow's stages, for the list filter (process-improvement flow). */
export const IMPROVEMENT_STAGES: { order: number; label: string }[] = [
    { order: 1, label: 'Planla' },
    { order: 2, label: 'Uygula' },
    { order: 3, label: 'Kontrol Et' },
    { order: 4, label: 'Önlem Al' },
];

/** Where the proposal stands in its flow; null when it is closed or rejected. */
export interface ImprovementStep {
    id: number;
    code: string;
    stageOrder: number;
    stageShortTitle: string;
}

/** A row of GET /api/v1/process-improvements (newest first). */
export interface ImprovementListItem {
    id: number;
    code: string;
    title: string;
    source: ImprovementSource;
    affectedProcess: string;
    priority: ImprovementPriority | null;
    priorityScore: number | null;
    status: ProjectStatus;
    currentStep: ImprovementStep | null;
    owner: UserSummary | null;
    createdAt: string;
}

export interface ImprovementPrioritization {
    impact: number | null;
    frequency: number | null;
    ease: number | null;
    score: number | null;
    priority: ImprovementPriority | null;
}

export interface ImprovementStudy {
    currentState: string | null;
    rootCause: string | null;
    solution: string | null;
    pilotPlan: string | null;
    pilotTarget: string | null;
    costBenefit: string | null;
}

export interface PilotEvaluation {
    pilotActual: string | null;
    pilotFeedback: string | null;
    pilotResult: PilotResult | null;
    pilotDecision: string | null;
}

/** GET /api/v1/process-improvements/{id}; id is the proposal's project id. */
export interface Improvement {
    id: number;
    code: string;
    status: ProjectStatus;
    currentStep: ImprovementStep | null;
    title: string;
    problem: string;
    source: ImprovementSource;
    affectedProcess: string;
    proposedSolution: string | null;
    suggestedBy: string;
    owner: UserSummary | null;
    prioritization: ImprovementPrioritization;
    study: ImprovementStudy;
    pilotEvaluation: PilotEvaluation;
    createdBy: UserSummary;
    createdAt: string;
    updatedAt: string | null;
}

/** POST and PUT /process-improvements/{id}: section 1. */
export interface ImprovementInput {
    title: string;
    problem: string;
    source: ImprovementSource;
    affectedProcess: string;
    proposedSolution: string | null;
    suggestedBy: string;
    ownerUserId: number;
}

/** All three scores or none (null empties them). */
export interface PrioritizationInput {
    impact: number | null;
    frequency: number | null;
    ease: number | null;
}

export interface ImprovementFilter {
    status?: ProjectStatus;
    stageOrder?: number;
    page?: number;
    pageSize?: number;
}

/** Score bands, the same as the API's ImprovementRules (3–15). */
export function priorityOf(score: number | null): ImprovementPriority | null {
    if (score === null) return null;
    if (score >= 12) return 'High';
    if (score >= 8) return 'Medium';
    return 'Low';
}
