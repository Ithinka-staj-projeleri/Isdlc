using Isdlc.Domain.Identity;
using Isdlc.Domain.Projects;

namespace Isdlc.Application.Process.Dtos;

/// <summary>One of the 13 stages with its steps in flow order.</summary>
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
    IReadOnlyList<ProcessStepJumpDto> NoJumps);

/// <summary>A step the flow can return to when the decision is answered "No".</summary>
public record ProcessStepJumpDto(
    int TargetStepId,
    string TargetStepCode,
    string Label);
