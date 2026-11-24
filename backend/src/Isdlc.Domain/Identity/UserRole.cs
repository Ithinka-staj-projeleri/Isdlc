namespace Isdlc.Domain.Identity;

public enum UserRole
{
    BusinessAnalyst,
    SoftwareDepartmentManager,
    ProjectManager,
    SoftwareDeveloper,
    TestSpecialist,
    SalesRepresentative,

    /// <summary>
    /// Platform Yöneticisi (Faz 6 · T4): opens and deactivates companies. Belongs to no company (TenantId empty) and sees
    /// no company's records. Never given from a form; only from configuration (docs/multi-tenancy.md).
    /// </summary>
    PlatformAdmin,
}
