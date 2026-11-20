using Isdlc.Application.Common.Models;
using Isdlc.Domain.Meetings;
using Isdlc.Domain.Process;

namespace Isdlc.Application.Audit.Dtos;

/// <summary>What happened; the order also breaks ties between events at the same moment.</summary>
public enum TimelineEventKind
{
    Created,
    Step,
    Meeting,
    Decision,
    RequirementApproved,
    RiskClosed,
}

/// <summary>
/// One event on a project's timeline (GET /api/v1/projects/{projectId}/timeline). Contract: docs/audit-api.md → Zaman çizelgesi.
/// RecordCode: the step code for a step, the project code for Created, otherwise the record's code.
/// DateOnly: only the day is known (a meeting); the screen does not show a time.
/// </summary>
public record TimelineEventDto(
    TimelineEventKind Kind,
    DateTime OccurredAt,
    bool DateOnly,
    UserSummaryDto PerformedBy,
    int? RecordId,
    string RecordCode,
    string Title,
    StepAction? StepAction,
    string? JumpedToStepCode,
    MeetingType? MeetingType,
    string? Note);
