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
