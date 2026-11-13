using Isdlc.Domain.Process;
using Isdlc.Domain.Projects;

namespace Isdlc.Application.Process.Dtos;

/// <summary>Where a project stands in its flow and what the signed-in user can do.</summary>
public record ProjectProcessDto(
    int ProjectId,
    string ProjectCode,
    ProcessFlowSummaryDto Flow,
    IReadOnlyList<int> SkippedStepIds, // steps of the flow whose conditions do not hold for this project
    ProjectStatus Status,
    ProcessStepDto? CurrentStep, // the step it is on (also while postponed); null when the project is closed
    int ProgressPercent,
    IReadOnlyList<ProcessStageProgressDto> Stages,
    AvailableActionsDto AvailableActions);

public record ProcessFlowSummaryDto(string Code, string Name, ProcessFlowKind Kind);

public record ProcessStageProgressDto(
    int StageId,
    int Order,
    string ShortTitle,
    int StepCount,
    ProcessStageState State);

public enum ProcessStageState
{
    Completed,
    Current,
    Pending,
    /// <summary>The project was closed in this stage without finishing the flow (Rejected, Lost, Redirected).</summary>
    Stopped,
}

/// <summary>Actions the signed-in user can take on the current step.</summary>
public record AvailableActionsDto(
    bool CanComplete,
    bool CanAnswer,
    bool CanReject,
    bool CanPostpone, // a request (before the project stage) can be put on hold
    bool CanResume,   // a postponed request can be taken back
    string? BlockedReason); // why the user cannot act on the current step; null when they can
