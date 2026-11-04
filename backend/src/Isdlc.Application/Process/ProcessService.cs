using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Process.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Isdlc.Application.Process;

public class ProcessService(IApplicationDbContext dbContext) : IProcessService
{
    public async Task<IReadOnlyList<ProcessStageDto>> GetStagesAsync(CancellationToken cancellationToken)
    {
        // Read-only query: AsNoTracking skips change tracking, and Select loads only the DTO fields.
        return await dbContext.ProcessStages
            .AsNoTracking()
            .OrderBy(stage => stage.Order)
            .Select(stage => new ProcessStageDto(
                stage.Id,
                stage.Order,
                stage.Title,
                stage.ShortTitle,
                stage.Steps
                    .OrderBy(step => step.Order)
                    .Select(step => new ProcessStepDto(
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
                            .Select(jump => new ProcessStepJumpDto(
                                jump.TargetStepId,
                                jump.TargetStep.Code,
                                jump.Label))
                            .ToList()))
                    .ToList()))
            .ToListAsync(cancellationToken);
    }
}
