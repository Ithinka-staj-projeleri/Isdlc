import { RiskCategory, RiskSeverity } from '../../risks/models/risk';

/**
 * POST /api/v1/projects/{projectId}/risks/suggestions — risks found in a text. Nothing is saved:
 * the person opens a suggestion in the risk form, edits it and saves it.
 */
export interface RiskSuggestions {
    items: SuggestedRisk[];
    /** Risks the model returned that did not pass validation. */
    skippedCount: number;
    modelName: string;
}

/** Category, probability, impact, score and severity are null when the model gave none (warnings say why). */
export interface SuggestedRisk {
    title: string;
    description: string | null;
    category: RiskCategory | null;
    probability: number | null;
    impact: number | null;
    score: number | null;
    severity: RiskSeverity | null;
    /** Why the model chose this probability and impact. */
    assessmentReason: string | null;
    mitigation: string | null;
    /** The sentence in the text the risk is based on. */
    evidence: string | null;
    warnings: string[];
}

/** POST /api/v1/projects/{projectId}/risks/assessment-suggestion — for the risk being typed in the form. */
export interface RiskAssessmentSuggestion {
    probability: number | null;
    impact: number | null;
    score: number | null;
    severity: RiskSeverity | null;
    assessmentReason: string | null;
    mitigation: string | null;
    warnings: string[];
    modelName: string;
}
