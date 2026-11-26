/**
 * Yetki (Faz 7 · A2/A3): doing a kind of work in the flow. A fixed, short list, the same in every company; a user can
 * have any number of them. Work outside the flow needs none. Contract: docs/configurability.md → Yetkiler.
 * The names are the backend's PermissionNames; the ready-made sets come from GET /api/v1/permissions.
 */
export type Permission = 'Analysis' | 'ProjectManagement' | 'Approval' | 'Sales' | 'Development' | 'Testing';

/** In the backend enum's order, which is also the order lists show. */
export const PERMISSIONS: readonly Permission[] = ['Analysis', 'ProjectManagement', 'Approval', 'Sales', 'Development', 'Testing'];

export const PERMISSION_LABELS: Record<Permission, string> = {
    Analysis: 'Analiz adımları',
    ProjectManagement: 'Proje yönetimi adımları',
    Approval: 'Onay ve karar',
    Sales: 'Teklif ve anlaşma',
    Development: 'Geliştirme',
    Testing: 'Test',
};

/** GET /api/v1/permissions: the list and the ready-made sets (today's titles; they only fill the boxes). */
export interface PermissionCatalog {
    permissions: { value: Permission; name: string }[];
    presets: PermissionPreset[];
}

export interface PermissionPreset {
    name: string;
    permissions: Permission[];
}

/** "Analiz adımları, Test" in the list's order; `empty` when there are none. */
export function permissionNames(permissions: readonly Permission[] | null | undefined, empty = 'Yetki yok'): string {
    const names = PERMISSIONS.filter(p => permissions?.includes(p)).map(p => PERMISSION_LABELS[p]);
    return names.length ? names.join(', ') : empty;
}

/** The preset whose permissions are exactly these, so the form can show which one is ticked; null when none is. */
export function matchingPreset(presets: readonly PermissionPreset[], permissions: readonly Permission[]): PermissionPreset | null {
    return presets.find(p => p.permissions.length === permissions.length && p.permissions.every(x => permissions.includes(x))) ?? null;
}

/** "Şirket yöneticinizden isteyin: Deniz Aydın, Mert Kaya." — the backend's PermissionMessages.AskAdmins. */
export function askAdmins(admins: readonly string[]): string {
    return admins.length ? `Şirket yöneticinizden isteyin: ${admins.join(', ')}.` : 'Şirket yöneticinizden isteyin.';
}

/** Shown in place of the request form to someone without "Analiz adımları" (karar 2b; same text as the 403). */
export function newRequestBlockedMessage(admins: readonly string[]): string {
    return `Talep açmak için «${PERMISSION_LABELS.Analysis}» yetkisi gerekir. ${askAdmins(admins)}`;
}

/**
 * A refusal split so the permission's name can be emphasised: "Bu adım için «Test» yetkisi gerekir…" →
 * ["Bu adım için ", "Test", " yetkisi gerekir…"]. A text without «…» is one part.
 */
export function splitPermissionName(text: string): { before: string; name: string | null; after: string } {
    const match = /«([^»]+)»/.exec(text);
    return match
        ? { before: text.slice(0, match.index), name: match[1], after: text.slice(match.index + match[0].length) }
        : { before: text, name: null, after: '' };
}
