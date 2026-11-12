using System.ComponentModel.DataAnnotations;
using Isdlc.Application.Meetings.Dtos;
using Isdlc.Domain.Meetings;
using Isdlc.Domain.OpenIssues;

namespace Isdlc.Application.Agents.Dtos;

/// <summary>
/// GET /api/v1/agents/status — which AI features the screen may offer. Both come from the same service today;
/// they are separate so a feature can be turned off on its own later.
/// </summary>
public record AgentStatusDto(bool MeetingAssistantEnabled, bool RiskAssistantEnabled);

/// <summary>POST .../meetings/suggestions — the raw meeting notes.</summary>
public record SuggestMeetingItemsDto
{
    [Required(ErrorMessage = "Toplantı tarihi zorunludur.")]
    public DateOnly? MeetingDate { get; init; }

    [Required(ErrorMessage = "Toplantı notları zorunludur.")]
    [MaxLength(MeetingSuggestionLimits.MaxNotes, ErrorMessage = "Notlar en fazla 20000 karakter olabilir.")]
    public string? Notes { get; init; }
}

public static class MeetingSuggestionLimits
{
    public const int MaxNotes = 20_000;
}

/// <summary>
/// Suggestions for the new minutes form, split the way the form uses them. Nothing is saved:
/// the person reviews and edits them in the form, then saves the minutes as usual.
/// </summary>
public record MeetingSuggestionsDto(
    string Summary,
    IReadOnlyList<SuggestedDecisionDto> Decisions,
    IReadOnlyList<SuggestedActionDto> Actions,
    IReadOnlyList<SuggestedOpenIssueDto> Others, // questions, dependencies...: no place in the form, shown for manual entry
    int SkippedCount, // items the model returned that did not pass validation; only counted
    string ModelName,
    MinutesDraftDto? Minutes = null); // null when the agent could not draft the minutes' info

/// <summary>
/// The minutes' info drafted from the notes: type, participants with roles, agenda (one "- " line per topic),
/// summary and next steps. The form fills only the fields the person has not filled yet.
/// </summary>
public record MinutesDraftDto(
    MeetingType Type,
    string? TypeReason,
    IReadOnlyList<MeetingParticipantDto> Participants,
    string Agenda,
    string? Summary,
    string? NextSteps,
    IReadOnlyList<string> Warnings);

public record SuggestedDecisionDto(string Title, string Evidence);

/// <summary>
/// An action. When the owner's name matches an active user, OwnerUserId is set and OwnerName is null;
/// otherwise OwnerName is the name from the notes (or null when the notes name nobody).
/// </summary>
public record SuggestedActionDto(
    string Title,
    int? OwnerUserId,
    string? OwnerName,
    DateOnly? DueDate,
    string Evidence,
    IReadOnlyList<string> Warnings);

public record SuggestedOpenIssueDto(
    OpenIssueType Type,
    string Title,
    string? OwnerName,
    DateOnly? DueDate,
    string Evidence);
