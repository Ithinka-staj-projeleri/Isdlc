using Isdlc.Domain.Projects;

namespace Isdlc.Application.Process.Dtos;

/// <summary>Where a project stands in the ISDLC flow and what the signed-in user can do.</summary>
public record ProjectProcessDto(
    int ProjectId,
    string ProjectCode,
    ProjectStatus Status,
    ProcessStepDto? CurrentStep, // null when the project is not active
    int ProgressPercent,
    IReadOnlyList<ProcessStageProgressDto> Stages,
    AvailableActionsDto AvailableActions);

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
    string? BlockedReason); // why the user cannot act on the current step; null when they can
