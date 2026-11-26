using Isdlc.Application.Common;
using Isdlc.Domain.Identity;

namespace Isdlc.Application.Identity;

/// <summary>
/// What a user without the needed permission is told, and whom to ask (karar 2b). Pure, so every wording is tested
/// without a database. The same text goes to the screen (BlockedReason) and to the 403.
/// Contract: docs/configurability.md → Yetkisi olmayan kullanıcı.
/// </summary>
public static class PermissionMessages
{
    public static string ForStep(Permission permission, IReadOnlyList<string> admins) =>
        $"Bu adım için «{permission.ToDisplayName()}» yetkisi gerekir. {AskAdmins(admins)}";

    public static string ForApproval(IReadOnlyList<string> admins) =>
        $"Bu adımı onaylamak için «{Permission.Approval.ToDisplayName()}» yetkisi gerekir. {AskAdmins(admins)}";

    public static string ForDecision(IReadOnlyList<string> admins) =>
        $"Talep hakkında karar vermek için «{Permission.Approval.ToDisplayName()}» yetkisi gerekir. {AskAdmins(admins)}";

    public static string ForNewRequest(IReadOnlyList<string> admins) =>
        $"Talep açmak için «{Permission.Analysis.ToDisplayName()}» yetkisi gerekir. {AskAdmins(admins)}";

    /// <summary>400 when the chosen analyst or project manager lacks the permission.</summary>
    public static string SelectedUserLacks(Permission permission) =>
        $"Seçilen kişinin «{permission.ToDisplayName()}» yetkisi yok.";

    /// <summary>"Şirket yöneticinizden isteyin: Deniz Aydın, Mert Kaya." — without names when the company has none.</summary>
    public static string AskAdmins(IReadOnlyList<string> admins) =>
        admins.Count == 0 ? "Şirket yöneticinizden isteyin." : $"Şirket yöneticinizden isteyin: {string.Join(", ", admins)}.";
}
