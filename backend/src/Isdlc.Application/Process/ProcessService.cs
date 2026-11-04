using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Process.Dtos;
using Isdlc.Domain.Identity;
using Isdlc.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace Isdlc.Application.Process;

public class ProcessService(IApplicationDbContext dbContext, ICurrentUserService currentUser) : IProcessService
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
                    .AsQueryable()
                    .OrderBy(step => step.Order)
                    .Select(ProcessMappings.ToStepDto)
                    .ToList()))
            .ToListAsync(cancellationToken);
    }

    public async Task<ProjectProcessDto> GetProjectProcessAsync(int projectId, CancellationToken cancellationToken)
    {
        var project = await dbContext.Projects
            .AsNoTracking()
            .Where(p => p.Id == projectId)
            .Select(p => new { p.Id, p.Code, p.Status, p.CurrentStepId })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Proje bulunamadı.");

        var isActive = project.Status == ProjectStatus.Active;

        // Active: the current step. Otherwise: the step where the project was closed (last record).
        var referenceStepId = isActive
            ? project.CurrentStepId
            : await dbContext.StepRecords
                .AsNoTracking()
                .Where(r => r.ProjectId == projectId)
                .OrderByDescending(r => r.PerformedAt)
                .ThenByDescending(r => r.Id)
                .Select(r => (int?)r.ProcessStepId)
                .FirstOrDefaultAsync(cancellationToken);

        var referenceStep = referenceStepId is null
            ? null
            : await dbContext.ProcessSteps
                .AsNoTracking()
                .Where(s => s.Id == referenceStepId)
                .Select(s => new
                {
                    s.Order,
                    StageOrder = s.ProcessStage.Order,
                    Permission = new StepPermissionInfo(s.IsDecision, s.ResponsibleRole, s.ApproverRole),
                })
                .SingleOrDefaultAsync(cancellationToken);

        var currentStep = isActive && project.CurrentStepId is not null
            ? await dbContext.ProcessSteps
                .AsNoTracking()
                .Where(s => s.Id == project.CurrentStepId)
                .Select(ProcessMappings.ToStepDto)
                .SingleOrDefaultAsync(cancellationToken)
            : null;

        var stages = await dbContext.ProcessStages
            .AsNoTracking()
            .OrderBy(stage => stage.Order)
            .Select(stage => new { stage.Id, stage.Order, stage.ShortTitle, StepCount = stage.Steps.Count })
            .ToListAsync(cancellationToken);

        var userRole = currentUser.UserId is null
            ? null
            : await dbContext.Users
                .AsNoTracking()
                .Where(u => u.Id == currentUser.UserId && u.IsActive)
                .Select(u => (UserRole?)u.Role)
                .SingleOrDefaultAsync(cancellationToken);

        var totalSteps = stages.Sum(stage => stage.StepCount);

        return new ProjectProcessDto(
            project.Id,
            project.Code,
            project.Status,
            currentStep,
            ProcessProgress.CalculateProgressPercent(project.Status, referenceStep?.Order, totalSteps),
            stages
                .Select(stage => new ProcessStageProgressDto(
                    stage.Id,
                    stage.Order,
                    stage.ShortTitle,
                    stage.StepCount,
                    ProcessProgress.GetStageState(project.Status, stage.Order, referenceStep?.StageOrder)))
                .ToList(),
            ProcessProgress.GetAvailableActions(
                project.Status, isActive ? referenceStep?.Permission : null, userRole));
    }
}
