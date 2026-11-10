using Isdlc.Domain.Identity;
using Isdlc.Domain.Projects;

namespace Isdlc.Domain.Notifications;

/// <summary>
/// One in-app notification for one user. A role notification is one row per active user of the role at sending time.
/// Not project data: not audited, never deleted. Rules: docs/data-model.md → Notification, docs/notifications-api.md
/// </summary>
public class Notification
{
    public const int MessageMaxLength = 500;

    public long Id { get; set; }

    /// <summary>Who receives it.</summary>
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>The project it is about; clicking the notification opens it.</summary>
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    /// <summary>The Turkish text written by the sending module.</summary>
    public string Message { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    /// <summary>When the user first read it; null while unread.</summary>
    public DateTime? ReadAt { get; set; }
}
