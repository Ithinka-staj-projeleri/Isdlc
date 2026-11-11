using Isdlc.Domain.Identity;
using Isdlc.Domain.Process;
using Isdlc.Domain.Projects;

namespace Isdlc.Application.Process.Dtos;

/// <summary>A flow without its steps (GET /process-flows).</summary>
public record ProcessFlowListItemDto(
    int Id,
    string Code,
    string Name,
    ProcessFlowKind Kind,
    int? ProjectStageOrder,
    int StepCount);

/// <summary>A flow with its stages and steps (GET /process-flows/{code}).</summary>
public record ProcessFlowDto(
    int Id,
    string Code,
    string Name,
    ProcessFlowKind Kind,
    int? ProjectStageOrder,
    IReadOnlyList<ProcessStageDto> Stages);

/// <summary>A stage of a flow with its steps in flow order.</summary>
public record ProcessStageDto(
    int Id,
    int Order,
    string Title,
    string ShortTitle,
    IReadOnlyList<ProcessStepDto> Steps);

public record ProcessStepDto(
    int Id,
    string Code,
    int Order,
    string Text,
    bool IsDecision,
    string? NoBranchText,
    UserRole ResponsibleRole,
    UserRole? ApproverRole,
    ProjectStatus? NoOutcome,
    IReadOnlyList<ProcessStepJumpDto> NoJumps,
    StepConditionDto Condition);

/// <summary>A step the flow can return to when the decision is answered "No".</summary>
public record ProcessStepJumpDto(
    int TargetStepId,
    string TargetStepCode,
    string Label);

/// <summary>When the step is done. All false / null: on every record. Rules: StepConditions.</summary>
public record StepConditionDto(
    bool RequiresExternalRequest,
    bool RequiresOffer,
    int? MinLevel);
