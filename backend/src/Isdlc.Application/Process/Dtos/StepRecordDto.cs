using Isdlc.Application.Common.Models;
using Isdlc.Domain.Process;

namespace Isdlc.Application.Process.Dtos;

/// <summary>One action taken on a step of a project (process history).</summary>
public record StepRecordDto(
    int Id,
    int StepId,
    string StepCode,
    StepAction Action,
    string? JumpedToStepCode, // only for a "No" answer that went back to an earlier step
    string? Note,
    UserSummaryDto PerformedBy,
    DateTime PerformedAt);
