using Isdlc.Domain.Identity;

namespace Isdlc.Application.Identity;

/// <summary>
/// Ready-made permission sets: today's titles, so the user form can tick the boxes with one click. Never saved
/// (docs/configurability.md → Hazır yetki setleri). The same table moved the titles to permissions (migration).
/// </summary>
public static class PermissionPresets
{
    public static readonly IReadOnlyList<(string Name, IReadOnlyList<Permission> Permissions)> All =
    [
        ("İş Analisti", [Permission.Analysis]),
        ("Yazılım Departman Yöneticisi", [Permission.Approval]),
        ("Proje Yöneticisi", [Permission.ProjectManagement]),
        ("Satış Temsilcisi", [Permission.Sales]),
        ("Yazılım Geliştirici", [Permission.Development]),
        ("Test Uzmanı", [Permission.Testing]),
    ];
}
