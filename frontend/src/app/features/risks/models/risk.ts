import { BadgeTone } from '../../../shared/components/status-badge/status-badge.component';
import { UserSummary } from '../../../shared/models/user-summary';

export type RiskCategory =
    | 'Technical' | 'Scope' | 'Stakeholder' | 'Schedule' | 'Cost' | 'Data' | 'SecurityPrivacy' | 'Supplier';
export type RiskStatus = 'Open' | 'Monitoring' | 'Closed';
export type RiskSeverity = 'Low' | 'Medium' | 'High';

export const RISK_CATEGORY_LABELS: Record<RiskCategory, string> = {
    Technical: 'Teknik',
    Scope: 'Kapsam',
    Stakeholder: 'Paydaş',
    Schedule: 'Takvim',
    Cost: 'Maliyet',
    Data: 'Veri',
    SecurityPrivacy: 'Güvenlik / KVKK',
    Supplier: 'Tedarikçi',
};

export const RISK_STATUS_LABELS: Record<RiskStatus, string> = {
    Open: 'Açık',
    Monitoring: 'İzleniyor',
    Closed: 'Kapandı',
};

export const RISK_SEVERITY_LABELS: Record<RiskSeverity, string> = {
    Low: 'Düşük',
    Medium: 'Orta',
    High: 'Yüksek',
};

export const RISK_SEVERITY_TONES: Record<RiskSeverity, BadgeTone> = {
    Low: 'ok',
    Medium: 'warn',
    High: 'bad',
};

export const RISK_STATUS_TONES: Record<RiskStatus, BadgeTone> = {
    Open: 'info',
    Monitoring: 'warn',
    Closed: 'neutral',
};

/** 1–7 Low, 8–14 Medium, 15–25 High: the same thresholds as the API (docs/risks-api.md → "Skor ve seviye"). */
export function severityOf(score: number): RiskSeverity {
    return score <= 7 ? 'Low' : score <= 14 ? 'Medium' : 'High';
}

/** A row of GET /api/v1/projects/{projectId}/risks. Score and severity are calculated by the API. */
export interface RiskListItem {
    id: number;
    code: string;
    title: string;
    category: RiskCategory;
    probability: number;
    impact: number;
    score: number;
    severity: RiskSeverity;
    mitigation: string | null;
    owner: UserSummary;
    isSharedWithRequester: boolean;
    status: RiskStatus;
}

export interface Risk extends RiskListItem {
    projectId: number;
    description: string | null;
    createdBy: UserSummary;
    createdAt: string;
    updatedAt: string | null;
}

/** probability and impact go together: a cell of the heat map. */
export interface RiskFilter {
    status?: RiskStatus;
    category?: RiskCategory;
    severity?: RiskSeverity;
    probability?: number;
    impact?: number;
}

export interface RiskMatrixCell {
    probability: number;
    impact: number;
    count: number;
}

/** Heat map of the risks that are not closed. Empty cells are not sent. */
export interface RiskMatrix {
    cells: RiskMatrixCell[];
    countBySeverity: { low: number; medium: number; high: number };
}

/** POST body; PUT adds the status. */
export interface SaveRisk {
    title: string;
    description: string | null;
    category: RiskCategory;
    probability: number;
    impact: number;
    mitigation: string | null;
    ownerUserId: number;
    isSharedWithRequester: boolean;
    status?: RiskStatus;
}
