using Isdlc.Application.Common.Models;
using Isdlc.Domain.Projects;

namespace Isdlc.Application.Process.Dtos;

/// <summary>
/// One filled-in Talep Ret / Erteleme Formu of a project (GET /projects/{projectId}/request-decisions).
/// Contract: docs/process-api.md → "Ret / erteleme kararı".
/// </summary>
public record RequestDecisionDto(
    int Id,
    RequestDecisionKind Decision,
    DateOnly DecisionDate,
    string DecidedBy,
    IReadOnlyList<DecisionReason> Reasons,
    string? ReasonOther,
    string Explanation,
    DateOnly? ReevaluationDate, // only for a postponement
    string StepCode,            // the step the request was on
    UserSummaryDto RecordedBy,  // who filled in the form
    DateTime RecordedAt);
