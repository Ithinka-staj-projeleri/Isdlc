using System.ComponentModel.DataAnnotations;
using Isdlc.Application.Common.Models;
using Isdlc.Application.OpenIssues.Dtos;
using Isdlc.Domain.Meetings;

namespace Isdlc.Application.Meetings.Dtos;

/// <summary>
/// A row of the meeting list. Counts come from the open issues created from the meeting:
/// decisions are Type = Decision, actions are all the others.
/// </summary>
public record MeetingListItemDto(
    int Id,
    string Code,
    MeetingType Type,
    DateOnly Date,
    DateOnly? ReturnDate,
    string ParticipantNames,
    int DecisionCount,
    int ActionCount,
    int OpenActionCount);

/// <summary>The minutes with their decisions and actions, as open issues with their current status.</summary>
public record MeetingDto(
    int Id,
    int ProjectId,
    string Code,
    MeetingType Type,
    DateOnly Date,
    DateOnly? ReturnDate,
    UserSummaryDto RecordedBy,
    IReadOnlyList<MeetingParticipantDto> Participants,
    string Agenda,
    string? Summary,
    string? CompletedStages,
    string? OngoingStages,
    string? NextSteps,
    string? PlannedActivities,
    string? CompletedActivities,
    IReadOnlyList<OpenIssueListItemDto> Decisions,
    IReadOnlyList<OpenIssueListItemDto> Actions,
    UserSummaryDto CreatedBy,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>Someone at the meeting. Role is free text: role, unit or company.</summary>
public record MeetingParticipantDto
{
    [Required(ErrorMessage = "Katılımcının adı zorunludur.")]
    [MaxLength(MeetingParticipant.MaxNameLength, ErrorMessage = "Katılımcının adı en fazla 100 karakter olabilir.")]
    public string? Name { get; init; }

    [MaxLength(MeetingParticipant.MaxRoleLength, ErrorMessage = "Rol en fazla 100 karakter olabilir.")]
    public string? Role { get; init; }
}

/// <summary>
/// PUT .../meetings/{id} — the minutes only; decisions and actions are managed as open issues.
/// ReturnDate and the activities are for a customer visit only (MeetingRules).
/// </summary>
public record UpdateMeetingDto
{
    // Nullable so a missing value is an error instead of silently becoming the default.
    [Required(ErrorMessage = "Toplantı türü zorunludur.")]
    public MeetingType? Type { get; init; }

    [Required(ErrorMessage = "Toplantı tarihi zorunludur.")]
    public DateOnly? Date { get; init; }

    public DateOnly? ReturnDate { get; init; }

    [Required(ErrorMessage = "Tutanağı yazan kişi seçilmelidir.")]
    public int? RecordedByUserId { get; init; }

    [Required(ErrorMessage = "En az bir katılımcı girilmelidir.")]
    [MinLength(1, ErrorMessage = "En az bir katılımcı girilmelidir.")]
    [MaxLength(MeetingParticipant.MaxCount, ErrorMessage = "En fazla 30 katılımcı girilebilir.")]
    public IReadOnlyList<MeetingParticipantDto>? Participants { get; init; }

    [Required(ErrorMessage = "Gündem zorunludur.")]
    [MaxLength(Meeting.MaxAgendaLength, ErrorMessage = "Gündem en fazla 4000 karakter olabilir.")]
    public string? Agenda { get; init; }

    [MaxLength(Meeting.MaxSummaryLength, ErrorMessage = "Toplantı özeti en fazla 4000 karakter olabilir.")]
    public string? Summary { get; init; }

    [MaxLength(Meeting.MaxStagesLength, ErrorMessage = "Tamamlanan aşamalar en fazla 2000 karakter olabilir.")]
    public string? CompletedStages { get; init; }

    [MaxLength(Meeting.MaxStagesLength, ErrorMessage = "Devam eden aşamalar en fazla 2000 karakter olabilir.")]
    public string? OngoingStages { get; init; }

    [MaxLength(Meeting.MaxNextStepsLength, ErrorMessage = "Sonraki adımlar en fazla 2000 karakter olabilir.")]
    public string? NextSteps { get; init; }

    [MaxLength(Meeting.MaxActivitiesLength, ErrorMessage = "Planlanan aktiviteler en fazla 4000 karakter olabilir.")]
    public string? PlannedActivities { get; init; }

    [MaxLength(Meeting.MaxActivitiesLength, ErrorMessage = "Yapılan aktiviteler en fazla 4000 karakter olabilir.")]
    public string? CompletedActivities { get; init; }
}

/// <summary>POST .../meetings — the decisions and actions become open issues of the project.</summary>
public record CreateMeetingDto : UpdateMeetingDto
{
    /// <summary>One decision per line; blank lines are skipped.</summary>
    [MaxLength(50, ErrorMessage = "En fazla 50 karar girilebilir.")]
    public IReadOnlyList<string?>? Decisions { get; init; }

    [MaxLength(50, ErrorMessage = "En fazla 50 aksiyon girilebilir.")]
    public IReadOnlyList<MeetingActionDto>? Actions { get; init; }
}

/// <summary>An action of the meeting. Same rules as an open issue: send exactly one of OwnerUserId and OwnerName.</summary>
public record MeetingActionDto
{
    [Required(ErrorMessage = "Aksiyon konusu zorunludur.")]
    [MaxLength(300, ErrorMessage = "Aksiyon konusu en fazla 300 karakter olabilir.")]
    public string? Title { get; init; }

    public int? OwnerUserId { get; init; }

    [MaxLength(200, ErrorMessage = "Sorumlu adı en fazla 200 karakter olabilir.")]
    public string? OwnerName { get; init; }

    public DateOnly? DueDate { get; init; }
}
