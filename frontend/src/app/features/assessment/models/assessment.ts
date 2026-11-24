import { BadgeTone } from '../../../shared/components/status-badge/status-badge.component';

/** Seviye & Uygunluk (Faz 4 · B2). Contract: docs/assessment-api.md → DTO'lar */

export const LEVEL_SET = 'level';
export const CUSTOMER_FIT_SET = 'customer-fit';

/** The level form's section codes and the names the form gives them. */
export const LEVEL_SECTION_TITLES: Record<string, string> = {
    '1': 'Doküman seviyesi',
    '2': 'Araştırma seviyesi',
};

/** Questionnaire.result of the level form. */
export interface LevelResult {
    documentLevel: { score: number; maxScore: number; answered: number; listed: number; level: number | null };
    research: { score: number; maxScore: number; answered: number; listed: number; days: number | null };
    appliedLevel: number | null;
    appliedResearchDays: number | null;
}

export type CustomerFitRating = 'Fit' | 'ConditionallyFit' | 'Risky' | 'NotFit';

/** Questionnaire.result of the customer fit survey. */
export interface CustomerFitResult {
    score: number;
    maxScore: number;
    answered: number;
    listed: number;
    rating: CustomerFitRating | null;
}

export const CUSTOMER_FIT_LABELS: Record<CustomerFitRating, string> = {
    Fit: 'Uygun',
    ConditionallyFit: 'Şartlı uygun',
    Risky: 'Riskli',
    NotFit: 'Uygun değil',
};

export const CUSTOMER_FIT_TONES: Record<CustomerFitRating, BadgeTone> = {
    Fit: 'ok',
    ConditionallyFit: 'warn',
    Risky: 'warn',
    NotFit: 'bad',
};

/** What the form says each rating means (form 006). */
export const CUSTOMER_FIT_DESCRIPTIONS: Record<CustomerFitRating, string> = {
    Fit: 'Yüksek olgunluk düzeyi, projeye doğrudan başlanabilir.',
    ConditionallyFit: 'Belirli konularda ön hazırlık ve danışmanlık gerekebilir.',
    Risky: 'Kritik eksikler mevcut, başarısı için aksiyon planı gerekir.',
    NotFit: 'Altyapı, ilgili veya stratejik uygunluk açısından yetersizlik.',
};

/** The suggested level or research days differ from what the project has (answers changed after applying). */
export function differsFromApplied(result: LevelResult): boolean {
    return result.documentLevel.level !== null
        && result.research.days !== null
        && (result.documentLevel.level !== result.appliedLevel || result.research.days !== result.appliedResearchDays);
}

/** Both sections are answered, so the level can be applied. */
export function canApplyLevel(result: LevelResult): boolean {
    return result.documentLevel.level !== null && result.research.days !== null;
}
