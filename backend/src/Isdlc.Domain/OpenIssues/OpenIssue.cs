using Isdlc.Domain.Common;
using Isdlc.Domain.Identity;
using Isdlc.Domain.Meetings;
using Isdlc.Domain.Projects;

namespace Isdlc.Domain.OpenIssues;

/// <summary>
/// A record on the project's Open Issues List (flow 6.3), shared with the requester:
/// question, action, decision, change request, dependency or assumption.
/// Never deleted; closed instead. Rules: docs/data-model.md → "Proje kayıtları".
/// </summary>
public class OpenIssue : AuditableEntity, IAudited, IProjectRecord
{
    public int Id { get; set; }

    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    /// <summary>"AK-001", increasing within the project.</summary>
    public string Code { get; set; } = string.Empty;

    public OpenIssueType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>A team member. Exactly one of OwnerUserId and OwnerName is set.</summary>
    public int? OwnerUserId { get; set; }
    public User? OwnerUser { get; set; }

    /// <summary>Someone outside the system ("Müşteri BT"). Exactly one of OwnerUserId and OwnerName is set.</summary>
    public string? OwnerName { get; set; }

    public DateOnly? DueDate { get; set; }
    public OpenIssueStatus Status { get; set; } = OpenIssueStatus.Open;

    /// <summary>Set when the status becomes Closed, cleared when it is reopened.</summary>
    public DateTime? ClosedAt { get; set; }

    /// <summary>The meeting whose decision or action this record came from; null when added directly.</summary>
    public int? SourceMeetingId { get; set; }
    public Meeting? SourceMeeting { get; set; }
}
