using Isdlc.Domain.Common;
using Isdlc.Domain.Identity;
using Isdlc.Domain.Projects;

namespace Isdlc.Domain.Meetings;

/// <summary>
/// Meeting minutes of a project. Its decisions and actions are not stored here as text:
/// they are saved as open issues linked by OpenIssue.SourceMeetingId.
/// A customer visit is minutes of type CustomerVisit, with the visit fields filled in.
/// Never deleted. Rules: docs/data-model.md → "Proje kayıtları".
/// </summary>
public class Meeting : AuditableEntity, IAudited, IProjectRecord
{
    public const int MaxAgendaLength = 4000;
    public const int MaxSummaryLength = 4000;
    public const int MaxStagesLength = 2000;
    public const int MaxNextStepsLength = 2000;
    public const int MaxActivitiesLength = 4000;

    public int Id { get; set; }

    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    /// <summary>"TT-001", increasing within the project.</summary>
    public string Code { get; set; } = string.Empty;

    public MeetingType Type { get; set; }

    /// <summary>The meeting date; the departure date of a visit.</summary>
    public DateOnly Date { get; set; }

    /// <summary>Who wrote the minutes.</summary>
    public int RecordedById { get; set; }
    public User RecordedBy { get; set; } = null!;

    /// <summary>In the order entered; stored as JSON.</summary>
    public List<MeetingParticipant> Participants { get; set; } = [];

    /// <summary>The agenda; "Ana gündem" of a visit.</summary>
    public string Agenda { get; set; } = string.Empty;

    /// <summary>Summary / notes of the meeting.</summary>
    public string? Summary { get; set; }

    /// <summary>Stages done at the time of the meeting; suggested from the process, kept as written.</summary>
    public string? CompletedStages { get; set; }

    /// <summary>Stages in progress at the time of the meeting.</summary>
    public string? OngoingStages { get; set; }

    public string? NextSteps { get; set; }

    /// <summary>Visit only: the return date, on or after Date.</summary>
    public DateOnly? ReturnDate { get; set; }

    /// <summary>Visit only.</summary>
    public string? PlannedActivities { get; set; }

    /// <summary>Visit only.</summary>
    public string? CompletedActivities { get; set; }
}
