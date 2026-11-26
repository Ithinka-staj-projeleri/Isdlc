import { UserRole } from '../../../shared/models/user-role';

// Contract: docs/multi-tenancy.md → Uç noktalar (Faz 6 · T4, T6).

/** A company as the Platform Administrator sees it: only how many users it has (backend: TenantDto). */
export interface Tenant {
    id: number;
    code: string;
    name: string;
    isActive: boolean;
    userCount: number;
    /** UTC, ISO 8601. */
    createdAt: string;
}

/** POST /tenants: the company, a copy of the templates and its first administrator, together. */
export interface CreateTenantInput {
    code: string;
    name: string;
    admin: {
        fullName: string;
        email: string;
        role: UserRole;
        temporaryPassword: string;
    };
}

/** PUT /tenants/{id}: the code never changes. */
export interface UpdateTenantInput {
    name: string;
    isActive: boolean;
}

/** GET / PUT /tenants/current/settings. */
export interface TenantSettings {
    aiEnabled: boolean;
    aiDataSharingAllowed: boolean;
    /** The company's model (Faz 7 · A9); null: the AI service's default. */
    aiModel: string | null;
    /** Input + output tokens a UTC month; null: no limit. */
    aiMonthlyTokenLimit: number | null;
}

export type AiAssistant = 'MeetingSuggestions' | 'RiskSuggestions' | 'RiskAssessment' | 'RequestDraft' | 'Transcription';

export const AI_ASSISTANT_LABELS: Record<AiAssistant, string> = {
    MeetingSuggestions: 'Tutanak önerileri',
    RiskSuggestions: 'Metinden risk önerisi',
    RiskAssessment: 'Risk puanı önerisi',
    RequestDraft: 'E-postadan talep taslağı',
    Transcription: 'Ses kaydından metin',
};

/** GET /tenants/current/ai-usage?month=2026-11 (Faz 7 · A9). */
export interface AiUsageSummary {
    /** "2026-11" (UTC month). */
    month: string;
    requests: number;
    failedRequests: number;
    inputTokens: number;
    outputTokens: number;
    audioSeconds: number;
    limit: number | null;
    byAssistant: { assistant: AiAssistant; requests: number; tokens: number }[];
}

/** Same rule as the backend's TenantRules: "Şirket kodu küçük harf, rakam ve tireden oluşmalıdır (2–50 karakter)." */
export const TENANT_CODE_PATTERN = /^[a-z0-9]+(-[a-z0-9]+)*$/;

export function isValidTenantCode(code: string): boolean {
    return code.length >= 2 && code.length <= 50 && TENANT_CODE_PATTERN.test(code);
}

/** "Örnek Lojistik A.Ş." → "ornek-lojistik-a-s": a suggestion for the code field; the user can change it. */
export function suggestTenantCode(name: string): string {
    const turkish: Record<string, string> = { ç: 'c', ğ: 'g', ı: 'i', İ: 'i', ö: 'o', ş: 's', ü: 'u' };
    return name
        .replace(/[çğıİöşü]/gi, ch => turkish[ch] ?? turkish[ch.toLowerCase()] ?? ch)
        .toLowerCase()
        .normalize('NFD')
        .replace(/[̀-ͯ]/g, '')
        .replace(/[^a-z0-9]+/g, '-')
        .replace(/^-+|-+$/g, '')
        .slice(0, 50)
        .replace(/-+$/g, '');
}
