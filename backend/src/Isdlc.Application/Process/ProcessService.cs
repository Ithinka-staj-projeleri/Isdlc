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
    public async Task<IReadOnlyList<ProcessFlowListItemDto>> GetFlowsAsync(CancellationToken cancellationToken)
    {
        return await dbContext.ProcessFlows
            .AsNoTracking()
            .OrderBy(flow => flow.Id)
            .Select(flow => new ProcessFlowListItemDto(
                flow.Id,
                flow.Code,
                flow.Name,
                flow.Kind,
                flow.ProjectStageOrder,
                dbContext.ProcessSteps.Count(step => step.ProcessFlowId == flow.Id)))
            .ToListAsync(cancellationToken);
    }

    public async Task<ProcessFlowDto> GetFlowAsync(string code, CancellationToken cancellationToken)
    {
        // Read-only query: AsNoTracking skips change tracking, and Select loads only the DTO fields.
        return await dbContext.ProcessFlows
            .AsNoTracking()
            .Where(flow => flow.Code == code)
            .Select(flow => new ProcessFlowDto(
                flow.Id,
                flow.Code,
                flow.Name,
                flow.Kind,
                flow.ProjectStageOrder,
                flow.Stages
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
                    .ToList()))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Akış bulunamadı.");
    }

    public async Task<ProjectProcessDto> GetProjectProcessAsync(int projectId, CancellationToken cancellationToken)
    {
        var project = await dbContext.Projects
            .AsNoTracking()
            .Where(p => p.Id == projectId)
            .Select(p => new
            {
                p.Id,
                p.Code,
                p.Status,
                p.CurrentStepId,
                p.ProcessFlowId,
                Flow = new ProcessFlowSummaryDto(p.ProcessFlow.Code, p.ProcessFlow.Name),
                Context = new ConditionContext(p.Source, p.RequiresOffer, p.Level),
            })
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
            .Where(stage => stage.ProcessFlowId == project.ProcessFlowId)
            .OrderBy(stage => stage.Order)
            .Select(stage => new { stage.Id, stage.Order, stage.ShortTitle, StepCount = stage.Steps.Count })
            .ToListAsync(cancellationToken);

        var flowSteps = await LoadFlowStepsAsync(project.ProcessFlowId, cancellationToken);
        var appliedOrders = flowSteps
            .Where(s => StepConditions.Applies(s.Condition, project.Context))
            .Select(s => s.Order)
            .ToList();

        // Position of the reference step among the steps done on the project; skipped steps do not count.
        int? referencePosition = referenceStep is null
            ? null
            : appliedOrders.Count(order => order < referenceStep.Order) + 1;

        var userRole = currentUser.UserId is null
            ? null
            : await dbContext.Users
                .AsNoTracking()
                .Where(u => u.Id == currentUser.UserId && u.IsActive)
                .Select(u => (UserRole?)u.Role)
                .SingleOrDefaultAsync(cancellationToken);

        return new ProjectProcessDto(
            project.Id,
            project.Code,
            project.Flow,
            StepConditions.SkippedStepIds(flowSteps, project.Context),
            project.Status,
            currentStep,
            ProcessProgress.CalculateProgressPercent(project.Status, referencePosition, appliedOrders.Count),
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
        var (project, step, user) = await LoadForCommandAsync(
            projectId, request.ExpectedStepId, ProcessProgress.WhyCannotActOnStep, cancellationToken);

        if (step.IsDecision)
        {
            throw new BusinessRuleException("Bu adım bir karar adımıdır; Evet/Hayır cevabı verilmelidir.");
        }

        var nextStep = await LoadNextStepAsync(project, step, cancellationToken);
        var transition = ProcessTransitions.AfterCompleted(nextStep?.Id);

        await SaveTransitionAsync(project, step, user, StepAction.Completed, request.Note, transition, cancellationToken);
        await NotifyAfterTransitionAsync(project, step, transition, nextStep, cancellationToken);

        return await GetProjectProcessAsync(projectId, cancellationToken);
    }

    public async Task<ProjectProcessDto> AnswerStepAsync(
        int projectId, AnswerStepDto request, CancellationToken cancellationToken)
    {
        var (project, step, user) = await LoadForCommandAsync(
            projectId, request.ExpectedStepId, ProcessProgress.WhyCannotActOnStep, cancellationToken);

        if (!step.IsDecision)
        {
            throw new BusinessRuleException("Bu adım bir karar adımı değil; Evet/Hayır cevabı verilmez, adım tamamlanır.");
        }

        // [Required] on the DTO guarantees a value.
        var answer = request.Answer!.Value;
        ProcessTransition transition;
        StepRef? targetStep;

        if (answer == StepAnswer.Yes)
        {
            if (request.JumpTargetStepId is not null)
            {
                throw new BusinessRuleException("Evet cevabında dönüş adımı seçilmez.");
            }

            targetStep = await LoadNextStepAsync(project, step, cancellationToken);
            transition = ProcessTransitions.AfterYes(targetStep?.Id);
        }
        else
        {
            var jumpTargetStepIds = await dbContext.ProcessStepJumps
                .AsNoTracking()
                .Where(j => j.ProcessStepId == step.Id)
                .Select(j => j.TargetStepId)
                .ToListAsync(cancellationToken);

            if (ProcessTransitions.ValidateNoAnswer(jumpTargetStepIds, request.JumpTargetStepId) is { } error)
            {
                throw new BusinessRuleException(error);
            }

            transition = ProcessTransitions.AfterNo(step.Id, step.NoOutcome, request.JumpTargetStepId);
            targetStep = transition.JumpedToStepId is { } jumpedTo
                ? await LoadStepRefAsync(jumpedTo, cancellationToken)
                : null;
        }

        var action = answer == StepAnswer.Yes ? StepAction.Yes : StepAction.No;
        await SaveTransitionAsync(project, step, user, action, request.Note, transition, cancellationToken);
        await NotifyAfterTransitionAsync(project, step, transition, targetStep, cancellationToken);

        return await GetProjectProcessAsync(projectId, cancellationToken);
    }

    public async Task<ProjectProcessDto> RejectAsync(
        int projectId, RejectProjectDto request, CancellationToken cancellationToken)
    {
        var (project, step, user) = await LoadForCommandAsync(
            projectId, request.ExpectedStepId, ProcessProgress.WhyCannotReject, cancellationToken);

        // The record is written on the step the request was on, with the reason as its note.
        var transition = ProcessTransitions.AfterRejected();
        await SaveTransitionAsync(project, step, user, StepAction.Rejected, request.Reason, transition, cancellationToken);
        await NotifyAfterTransitionAsync(project, step, transition, null, cancellationToken);

        return await GetProjectProcessAsync(projectId, cancellationToken);
    }

    // ---- Shared by the commands (complete, answer, reject) ----

    private record CurrentStepInfo(
        int Id, string Code, int Order, bool IsDecision, ProjectStatus? NoOutcome, StepPermissionInfo Permission);

    /// <summary>A step the project moves to: enough to save the move and send notifications.</summary>
    private record StepRef(int Id, string Code, UserRole? ApproverRole);

    /// <summary>
    /// The next step of the project's flow whose conditions hold (StepConditions); null after the last one.
    /// Conditions are checked with the project's values as they are now.
    /// </summary>
    private async Task<StepRef?> LoadNextStepAsync(
        Project project, CurrentStepInfo step, CancellationToken cancellationToken)
    {
        var flowSteps = await LoadFlowStepsAsync(project.ProcessFlowId, cancellationToken);
        var context = new ConditionContext(project.Source, project.RequiresOffer, project.Level);

        return StepConditions.NextStepId(flowSteps, step.Order, context) is { } nextStepId
            ? await LoadStepRefAsync(nextStepId, cancellationToken)
            : null;
    }

    /// <summary>Every step of the flow with its order and conditions (a flow has at most a few dozen steps).</summary>
    private async Task<IReadOnlyList<ConditionalStep>> LoadFlowStepsAsync(
        int flowId, CancellationToken cancellationToken) =>
        await dbContext.ProcessSteps
            .AsNoTracking()
            .Where(s => s.ProcessFlowId == flowId)
            .OrderBy(s => s.Order)
            .Select(s => new ConditionalStep(
                s.Id, s.Order, new StepCondition(s.RequiresExternalRequest, s.RequiresOffer, s.MinLevel)))
            .ToListAsync(cancellationToken);

    private Task<StepRef?> LoadStepRefAsync(int stepId, CancellationToken cancellationToken) =>
        dbContext.ProcessSteps
            .AsNoTracking()
            .Where(s => s.Id == stepId)
            .Select(s => new StepRef(s.Id, s.Code, s.ApproverRole))
            .SingleOrDefaultAsync(cancellationToken);

    private record ActingUser(int Id, UserRole Role);

    /// <summary>
    /// Loads the project (tracked, so RowVersion is checked on save), its current step and the acting user,
    /// and runs the checks every command shares, in this order: 404, 409 not active, 409 step changed, 403 role.
    /// whyForbidden is the command's own permission rule (ProcessProgress.WhyCannot...): null means allowed.
    /// </summary>
    private async Task<(Project Project, CurrentStepInfo Step, ActingUser User)> LoadForCommandAsync(
        int projectId, int expectedStepId, Func<StepPermissionInfo, UserRole, string?> whyForbidden,
        CancellationToken cancellationToken)
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
                s.Id, s.Code, s.Order, s.IsDecision, s.NoOutcome,
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

        if (whyForbidden(step.Permission, user.Role) is { } reason)
        {
            throw new ForbiddenException(reason);
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
            JumpedToStepId = transition.JumpedToStepId,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            PerformedById = user.Id,
            PerformedAt = timeProvider.GetUtcNow().UtcDateTime,
        });

        project.Status = transition.Status;
        project.CurrentStepId = transition.CurrentStepId;

        // A "No" that stays on the same step changes nothing on the project, so EF would not update it
        // and RowVersion would not be checked. Marking it modified always sends the UPDATE with the
        // RowVersion check, so a record is never saved on a step another user has just moved past.
        // The audit log skips fields whose value did not change.
        dbContext.Entry(project).Property(p => p.CurrentStepId).IsModified = true;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Notifications after a successful save: closing → analyst and project manager;
    /// going back → analyst; arriving at an approval step → users with the approver role.
    /// </summary>
    private async Task NotifyAfterTransitionAsync(
        Project project, CurrentStepInfo fromStep, ProcessTransition transition, StepRef? toStep,
        CancellationToken cancellationToken)
    {
        if (transition.Status != ProjectStatus.Active)
        {
            await NotifyClosedAsync(project, cancellationToken);
            return;
        }

        if (toStep is null || toStep.Id == fromStep.Id)
        {
            return;
        }

        if (transition.JumpedToStepId is not null && project.BusinessAnalystId is { } analystId)
        {
            await notifications.NotifyUsersAsync(
                [analystId], project.Id,
                $"{project.Code} · {fromStep.Code} adımından {toStep.Code} adımına dönüldü.", cancellationToken);
        }

        if (toStep.ApproverRole is { } approver)
        {
            await notifications.NotifyRoleAsync(
                approver, project.Id, $"{project.Code} · {toStep.Code} onayınızı bekliyor.", cancellationToken);
        }
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
