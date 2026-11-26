/** UserRole as the API sends it (enum as text). */
export type UserRole =
    | 'BusinessAnalyst'
    | 'SoftwareDepartmentManager'
    | 'ProjectManager'
    | 'SoftwareDeveloper'
    | 'TestSpecialist'
    | 'SalesRepresentative'
    /** Opens and closes companies; has no company of its own (docs/multi-tenancy.md). Never given from a form. */
    | 'PlatformAdmin';

/** Turkish role names shown on screen (same as the backend's UserRoleNames). */
export const USER_ROLE_LABELS: Record<UserRole, string> = {
    BusinessAnalyst: 'İş Analisti',
    SoftwareDepartmentManager: 'Yazılım Departman Yöneticisi',
    ProjectManager: 'Proje Yöneticisi',
    SoftwareDeveloper: 'Yazılım Geliştirici',
    TestSpecialist: 'Test Uzmanı',
    SalesRepresentative: 'Satış Temsilcisi',
    PlatformAdmin: 'Platform Yöneticisi',
};

/** Roles a company's user can be given (user and new company forms); PlatformAdmin comes only from configuration. */
export const ASSIGNABLE_ROLES: UserRole[] = (Object.keys(USER_ROLE_LABELS) as UserRole[])
    .filter(role => role !== 'PlatformAdmin');

/**
 * Faz 7 · A2 bridge, removed by A3: the API now sends permissions in place of the title. Until the screens show
 * permissions, a user's title is worked out from them with the same table the migration used, so the old screens work.
 */
export type Permission = 'Analysis' | 'ProjectManagement' | 'Approval' | 'Sales' | 'Development' | 'Testing';

const PERMISSION_OF_ROLE: Partial<Record<UserRole, Permission>> = {
    BusinessAnalyst: 'Analysis',
    SoftwareDepartmentManager: 'Approval',
    ProjectManager: 'ProjectManagement',
    SalesRepresentative: 'Sales',
    SoftwareDeveloper: 'Development',
    TestSpecialist: 'Testing',
};

/** The title an old screen shows for a user's permissions (the first in the table's order). */
export function roleFromPermissions(permissions: readonly string[] | undefined, isPlatformAdmin?: boolean): UserRole {
    if (isPlatformAdmin) return 'PlatformAdmin';
    const found = (Object.keys(PERMISSION_OF_ROLE) as UserRole[]).find(r => permissions?.includes(PERMISSION_OF_ROLE[r]!));
    return found ?? 'SoftwareDeveloper';
}

/** The permission list an old form sends for the title it shows. */
export function permissionsFromRole(role: UserRole | null | undefined): Permission[] {
    const permission = role ? PERMISSION_OF_ROLE[role] : undefined;
    return permission ? [permission] : [];
}
