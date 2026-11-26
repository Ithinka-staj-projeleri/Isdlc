using Isdlc.Domain.Identity;

namespace Isdlc.Application.Common;

/// <summary>Turkish permission names, the only place they are written (docs/configurability.md → Yetki listesi).</summary>
public static class PermissionNames
{
    public static string ToDisplayName(this Permission permission) => permission switch
    {
        Permission.Analysis => "Analiz adımları",
        Permission.ProjectManagement => "Proje yönetimi adımları",
        Permission.Approval => "Onay ve karar",
        Permission.Sales => "Teklif ve anlaşma",
        Permission.Development => "Geliştirme",
        Permission.Testing => "Test",
        _ => permission.ToString(),
    };
}
