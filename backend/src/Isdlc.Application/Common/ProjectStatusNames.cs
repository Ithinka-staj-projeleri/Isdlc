using Isdlc.Domain.Projects;

namespace Isdlc.Application.Common;

/// <summary>Turkish project status names for messages shown to the user.</summary>
public static class ProjectStatusNames
{
    public static string ToDisplayName(this ProjectStatus status) => status switch
    {
        ProjectStatus.Active => "Aktif",
        ProjectStatus.Closed => "Kapandı",
        ProjectStatus.Rejected => "Reddedildi",
        ProjectStatus.Lost => "Kaybedildi",
        ProjectStatus.Redirected => "Yönlendirildi",
        _ => status.ToString(),
    };
}
