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
    string Participants,
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
    string Participants,
    string Agenda,
    IReadOnlyList<OpenIssueListItemDto> Decisions,
    IReadOnlyList<OpenIssueListItemDto> Actions,
    UserSummaryDto CreatedBy,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>PUT .../meetings/{id} — the minutes only; decisions and actions are managed as open issues.</summary>
public record UpdateMeetingDto
{
    // Nullable so a missing value is an error instead of silently becoming the default.
    [Required(ErrorMessage = "Toplantı türü zorunludur.")]
    public MeetingType? Type { get; init; }

    [Required(ErrorMessage = "Toplantı tarihi zorunludur.")]
    public DateOnly? Date { get; init; }

    [Required(ErrorMessage = "Katılımcılar zorunludur.")]
    [MaxLength(1000, ErrorMessage = "Katılımcılar en fazla 1000 karakter olabilir.")]
    public string? Participants { get; init; }

    [Required(ErrorMessage = "Gündem zorunludur.")]
    [MaxLength(4000, ErrorMessage = "Gündem en fazla 4000 karakter olabilir.")]
    public string? Agenda { get; init; }
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
