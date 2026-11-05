using Isdlc.Application.Common;
using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Common.Models;
using Isdlc.Application.Notifications;
using Isdlc.Application.Process.Dtos;
using Isdlc.Domain.Identity;
using Isdlc.Domain.Process;
using Isdlc.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace Isdlc.Application.Process;

public class ProcessService(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUser,
    INotificationService notifications,
    TimeProvider timeProvider) : IProcessService
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

    public async Task<PagedResult<StepRecordDto>> GetStepRecordsAsync(
        int projectId, PageRequest pageRequest, CancellationToken cancellationToken)
    {
        // An empty list would hide a wrong id, so a missing project is a 404.
        if (!await dbContext.Projects.AnyAsync(p => p.Id == projectId, cancellationToken))
        {
            throw new NotFoundException("Proje bulunamadı.");
        }

        var records = dbContext.StepRecords
            .AsNoTracking()
            .Where(r => r.ProjectId == projectId);

        var totalCount = await records.CountAsync(cancellationToken);

        var items = await records
            .OrderByDescending(r => r.PerformedAt)
            .ThenByDescending(r => r.Id)
            .ApplyPaging(pageRequest)
            .Select(r => new StepRecordDto(
                r.Id,
                r.ProcessStepId,
                r.ProcessStep.Code,
                r.Action,
                r.JumpedToStep != null ? r.JumpedToStep.Code : null,
                r.Note,
                new UserSummaryDto(r.PerformedBy.Id, r.PerformedBy.FullName),
                r.PerformedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<StepRecordDto>(items, totalCount);
    }

    public async Task<ProjectProcessDto> CompleteStepAsync(
        int projectId, CompleteStepDto request, CancellationToken cancellationToken)
    {
        var (project, step, user) = await LoadForCommandAsync(projectId, request.ExpectedStepId, cancellationToken);

        if (step.IsDecision)
        {
            throw new BusinessRuleException("Bu adım bir karar adımıdır; Evet/Hayır cevabı verilmelidir.");
        }

        var nextStep = await dbContext.ProcessSteps
            .AsNoTracking()
            .Where(s => s.Order == step.Order + 1)
            .Select(s => new NextStepInfo(s.Id, s.Code, s.ApproverRole))
            .SingleOrDefaultAsync(cancellationToken);

        var transition = ProcessTransitions.AfterCompleted(nextStep?.Id);
        await SaveTransitionAsync(project, step, user, StepAction.Completed, request.Note, transition, cancellationToken);

        if (transition.Status != ProjectStatus.Active)
        {
            await NotifyClosedAsync(project, cancellationToken);
        }
        else if (nextStep?.ApproverRole is { } approver)
        {
            await notifications.NotifyRoleAsync(
                approver, project.Id, $"{project.Code} · {nextStep.Code} onayınızı bekliyor.", cancellationToken);
        }

        return await GetProjectProcessAsync(projectId, cancellationToken);
    }

    // ---- Shared by the commands (complete, answer, reject) ----

    private record CurrentStepInfo(int Id, string Code, int Order, bool IsDecision, StepPermissionInfo Permission);

    private record NextStepInfo(int Id, string Code, UserRole? ApproverRole);

    private record ActingUser(int Id, UserRole Role);

    /// <summary>
    /// Loads the project (tracked, so RowVersion is checked on save), its current step and the acting user,
    /// and runs the checks every command shares, in this order: 404, 409 not active, 409 step changed, 403 role.
    /// </summary>
    private async Task<(Project Project, CurrentStepInfo Step, ActingUser User)> LoadForCommandAsync(
        int projectId, int expectedStepId, CancellationToken cancellationToken)
    {
        var project = await dbContext.Projects.SingleOrDefaultAsync(p => p.Id == projectId, cancellationToken)
            ?? throw new NotFoundException("Proje bulunamadı.");

        if (project.Status != ProjectStatus.Active || project.CurrentStepId is null)
        {
            throw new ConflictException("Proje aktif değil; süreçte işlem yapılamaz.");
        }

        if (project.CurrentStepId != expectedStepId)
        {
            throw new ConflictException("Proje bu arada başka bir adıma geçti. Sayfayı yenileyin.");
        }

        var step = await dbContext.ProcessSteps
            .AsNoTracking()
            .Where(s => s.Id == project.CurrentStepId)
            .Select(s => new CurrentStepInfo(
                s.Id, s.Code, s.Order, s.IsDecision,
                new StepPermissionInfo(s.IsDecision, s.ResponsibleRole, s.ApproverRole)))
            .SingleAsync(cancellationToken);

        var user = currentUser.UserId is null
            ? null
            : await dbContext.Users
                .AsNoTracking()
                .Where(u => u.Id == currentUser.UserId && u.IsActive)
                .Select(u => new ActingUser(u.Id, u.Role))
                .SingleOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            throw new ForbiddenException("İşlem yapmak için giriş yapılmalıdır.");
        }

        if (!ProcessProgress.CanActOnStep(step.Permission, user.Role))
        {
            throw new ForbiddenException(ProcessProgress.GetBlockedReason(step.Permission));
        }

        return (project, step, user);
    }

    /// <summary>
    /// Writes the StepRecord and moves the project in one SaveChanges: both are saved or neither.
    /// If someone else changed the project after it was loaded, RowVersion makes SaveChanges fail (409).
    /// </summary>
    private async Task SaveTransitionAsync(
        Project project, CurrentStepInfo step, ActingUser user, StepAction action, string? note,
        ProcessTransition transition, CancellationToken cancellationToken)
    {
        dbContext.StepRecords.Add(new StepRecord
        {
            ProjectId = project.Id,
            ProcessStepId = step.Id,
            Action = action,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            PerformedById = user.Id,
            PerformedAt = timeProvider.GetUtcNow().UtcDateTime,
        });

        project.Status = transition.Status;
        project.CurrentStepId = transition.CurrentStepId;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task NotifyClosedAsync(Project project, CancellationToken cancellationToken)
    {
        var userIds = new[] { project.BusinessAnalystId, project.ProjectManagerId }
            .OfType<int>()
            .Distinct()
            .ToList();

        if (userIds.Count > 0)
        {
            await notifications.NotifyUsersAsync(
                userIds, project.Id, $"{project.Code} kapandı: {project.Status.ToDisplayName()}.", cancellationToken);
        }
    }
}
