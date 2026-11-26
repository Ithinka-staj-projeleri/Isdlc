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
        temporaryPassword: string;
    };
}

/** PUT /tenants/{id}: the code never changes. */
export interface UpdateTenantInput {
    name: string;
    isActive: boolean;
}

/** GET /tenants/current: the user's company and its active administrators' names ("whom to ask"). */
export interface CurrentTenant {
    id: number;
    code: string;
    name: string;
    admins: string[];
}

/** GET / PUT /tenants/current/settings. */
export interface TenantSettings {
    aiEnabled: boolean;
    aiDataSharingAllowed: boolean;
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
