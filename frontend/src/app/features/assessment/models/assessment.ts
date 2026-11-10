import { BadgeTone } from '../../../shared/components/status-badge/status-badge.component';
import { UserSummary } from '../../../shared/models/user-summary';

export type AcceptanceRecommendation = 'Accept' | 'Conditional' | 'RejectOrPostpone';

export const RECOMMENDATION_LABELS: Record<AcceptanceRecommendation, string> = {
    Accept: 'Kabul önerilir',
    Conditional: 'Koşullu kabul',
    RejectOrPostpone: 'Ret / erteleme önerilir',
};

export const RECOMMENDATION_TONES: Record<AcceptanceRecommendation, BadgeTone> = {
    Accept: 'ok',
    Conditional: 'warn',
    RejectOrPostpone: 'bad',
};

/** Keys are the JSON names; the definition sends the same keys. Null means not answered yet. */
export type LevelAnswers = Record<string, number | null>;
export type AcceptanceScores = Record<string, number | null>;

export interface ProjectAssessment {
    projectId: number;
    level: {
        answers: LevelAnswers;
        answeredCount: number;
        score: number | null;
        suggestedLevel: number | null;
        appliedLevel: number | null;
    };
    acceptance: {
        scores: AcceptanceScores;
        answeredCount: number;
        weightedScore: number | null;
        recommendation: AcceptanceRecommendation | null;
    };
    updatedBy: UserSummary | null;
    updatedAt: string | null;
}

/** GET /api/v1/assessment/definition: questions, criteria and rules, written once in the backend. */
export interface AssessmentDefinition {
    levelQuestions: { key: string; title: string; options: { value: number; label: string }[] }[];
    levelRule: string;
    acceptanceCriteria: { key: string; title: string; weight: number }[];
    acceptanceRule: string;
}
