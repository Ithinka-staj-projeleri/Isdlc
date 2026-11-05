using Isdlc.Domain.Identity;

namespace Isdlc.Application.Common;

/// <summary>Turkish role names for messages shown to the user.</summary>
public static class UserRoleNames
{
    public static string ToDisplayName(this UserRole role) => role switch
    {
        UserRole.BusinessAnalyst => "İş Analisti",
        UserRole.SoftwareDepartmentManager => "Yazılım Departman Yöneticisi",
        UserRole.ProjectManager => "Proje Yöneticisi",
        UserRole.SoftwareDeveloper => "Yazılım Geliştirici",
        UserRole.TestSpecialist => "Test Uzmanı",
        UserRole.SalesRepresentative => "Satış Temsilcisi",
        _ => role.ToString(),
    };
}
