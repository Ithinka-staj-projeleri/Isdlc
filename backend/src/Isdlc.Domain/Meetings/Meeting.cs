using Isdlc.Domain.Common;
using Isdlc.Domain.Projects;

namespace Isdlc.Domain.Meetings;

/// <summary>
/// Meeting minutes of a project. Its decisions and actions are not stored here as text:
/// they are saved as open issues linked by OpenIssue.SourceMeetingId.
/// Never deleted. Rules: docs/data-model.md → "Proje kayıtları".
/// </summary>
public class Meeting : AuditableEntity, IAudited, IProjectRecord
{
    public int Id { get; set; }

    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    /// <summary>"TT-001", increasing within the project.</summary>
    public string Code { get; set; } = string.Empty;

    public MeetingType Type { get; set; }
    public DateOnly Date { get; set; }

    /// <summary>Free text: "Elif Yılmaz, Can Öztürk, Müşteri BT".</summary>
    public string Participants { get; set; } = string.Empty;

    public string Agenda { get; set; } = string.Empty;
}
