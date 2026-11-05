using System.Linq.Expressions;
using Isdlc.Application.Process.Dtos;
using Isdlc.Domain.Process;

namespace Isdlc.Application.Process;

/// <summary>
/// Entity → DTO conversions written as expressions, so EF Core turns them into SQL
/// and only the needed columns are read.
/// </summary>
public static class ProcessMappings
{
    public static readonly Expression<Func<ProcessStep, ProcessStepDto>> ToStepDto = step => new ProcessStepDto(
        step.Id,
        step.Code,
        step.Order,
        step.Text,
        step.IsDecision,
        step.NoBranchText,
        step.ResponsibleRole,
        step.ApproverRole,
        step.NoOutcome,
        step.NoJumps
            .OrderBy(jump => jump.TargetStep.Order)
            .Select(jump => new ProcessStepJumpDto(jump.TargetStepId, jump.TargetStep.Code, jump.Label))
            .ToList());
}
