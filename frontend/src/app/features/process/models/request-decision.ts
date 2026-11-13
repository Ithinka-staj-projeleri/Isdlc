// The Talep Ret / Erteleme Formu (ISDLC-Form-Analiz-007). Contract: docs/process-api.md → "Ret / erteleme kararı".

export type RequestDecisionKind = 'Reject' | 'Postpone';

export type DecisionReason =
    | 'StrategicMisfit'
    | 'InsufficientValue'
    | 'BudgetConstraint'
    | 'CapacityShortage'
    | 'TechnicalInfeasibility'
    | 'LegalNonCompliance'
    | 'UnacceptableRisk'
    | 'Prioritization'
    | 'InsufficientInformation'
    | 'Other';

export const REQUEST_DECISION_LABELS: Record<RequestDecisionKind, string> = {
    Reject: 'Ret',
    Postpone: 'Erteleme',
};

/** The form's reason categories, in its order and words. */
export const DECISION_REASONS: { value: DecisionReason; label: string; description: string }[] = [
    { value: 'StrategicMisfit', label: 'Stratejik uyumsuzluk', description: 'Talep, kurumun stratejik hedefleri ve öncelikleri ile uyumlu bulunmamıştır.' },
    { value: 'InsufficientValue', label: 'Yetersiz iş değeri / fayda', description: 'Beklenen fayda, tahmini maliyet ve efor karşısında yeterli görülmemiştir (maliyet-fayda analizi).' },
    { value: 'BudgetConstraint', label: 'Bütçe kısıtı', description: 'Talebin karşılanması için gerekli bütçe ayrılamamaktadır.' },
    { value: 'CapacityShortage', label: 'Kaynak / kapasite yetersizliği', description: 'Talebin karşılanması için gerekli insan kaynağı veya yetkinlik mevcut değildir.' },
    { value: 'TechnicalInfeasibility', label: 'Teknik uygulanabilirlik', description: 'Talep, mevcut teknoloji, mimari veya altyapı kısıtları nedeniyle uygulanabilir bulunmamıştır.' },
    { value: 'LegalNonCompliance', label: 'Yasal / mevzuat uyumu', description: 'Talep, yasal düzenlemeler, sözleşme şartları veya kurum politikaları ile uyumlu değildir.' },
    { value: 'UnacceptableRisk', label: 'Kabul edilemez risk', description: 'Talebe ilişkin risklerin etkisi ve olasılığı kabul edilebilir düzeyin üzerinde değerlendirilmiştir.' },
    { value: 'Prioritization', label: 'Önceliklendirme', description: 'Daha yüksek öncelikli talepler nedeniyle talep plana alınamamıştır.' },
    { value: 'InsufficientInformation', label: 'Yetersiz / belirsiz talep bilgisi', description: 'Talebin değerlendirilmesi için gerekli bilgi ve gereksinimler yeterli netlikte iletilmemiştir.' },
    { value: 'Other', label: 'Diğer', description: '' },
];

export const DECISION_REASON_LABELS = Object.fromEntries(DECISION_REASONS.map(r => [r.value, r.label])) as Record<DecisionReason, string>;

/** What the form sends (reject, postpone and the "No" that closes a request). */
export interface RequestDecisionForm {
    /** Date only, e.g. "2026-11-13". */
    decisionDate: string;
    decidedBy: string;
    reasons: DecisionReason[];
    /** Only with "Other". */
    reasonOther: string | null;
    explanation: string;
}

/** GET /api/v1/projects/{projectId}/request-decisions — newest first. */
export interface RequestDecision {
    id: number;
    decision: RequestDecisionKind;
    decisionDate: string;
    decidedBy: string;
    reasons: DecisionReason[];
    reasonOther: string | null;
    explanation: string;
    /** Only for a postponement. */
    reevaluationDate: string | null;
    stepCode: string;
    recordedBy: { id: number; fullName: string };
    recordedAt: string;
}
