/** UserRole as the API sends it (enum as text). */
export type UserRole =
    | 'BusinessAnalyst'
    | 'SoftwareDepartmentManager'
    | 'ProjectManager'
    | 'SoftwareDeveloper'
    | 'TestSpecialist'
    | 'SalesRepresentative';

/** Turkish role names shown on screen (same as the backend's UserRoleNames). */
export const USER_ROLE_LABELS: Record<UserRole, string> = {
    BusinessAnalyst: 'İş Analisti',
    SoftwareDepartmentManager: 'Yazılım Departman Yöneticisi',
    ProjectManager: 'Proje Yöneticisi',
    SoftwareDeveloper: 'Yazılım Geliştirici',
    TestSpecialist: 'Test Uzmanı',
    SalesRepresentative: 'Satış Temsilcisi',
};
