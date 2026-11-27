using Isdlc.Application.Common;
using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Common.Models;
using Isdlc.Application.Identity;
using Isdlc.Application.Notifications;
using Isdlc.Application.Process.Dtos;
using Isdlc.Application.Projects;
using Isdlc.Application.QuestionBank;
using Isdlc.Domain.Documents;
using Isdlc.Domain.Identity;
using Isdlc.Domain.Process;
using Isdlc.Domain.Projects;
using Isdlc.Domain.Requirements;
using Microsoft.EntityFrameworkCore;

namespace Isdlc.Application.Process;

public class ProcessService(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUser,
    INotificationService notifications,
    TimeProvider timeProvider,
    IQuestionnaireService questionnaires) : IProcessService
{
    public async Task<IReadOnlyList<ProcessFlowListItemDto>> GetFlowsAsync(CancellationToken cancellationToken)
    {
        return await dbContext.ProcessFlows
            .AsNoTracking()
            .OrderBy(flow => flow.Order)
            .Select(flow => new ProcessFlowListItemDto(
                flow.Id,
                flow.Code,
                flow.Name,
                flow.Kind,
                flow.ProjectStageOrder,
                dbContext.ProcessSteps.Count(step => step.ProcessFlowId == flow.Id && step.IsActive),
                flow.UsedFor,
                flow.IsCustomized))
            .ToListAsync(cancellationToken);
    }

    public async Task<ProcessFlowDto> GetFlowAsync(string code, bool includeInactive, CancellationToken cancellationToken)
    {
        // Inactive stages and steps are the administrator's to see (flow editing); everyone else reads the flow as it runs.
        var all = includeInactive && currentUser.IsTenantAdmin;

        // Read-only query: AsNoTracking skips change tracking, and Select loads only the DTO fields.
        var flow = await dbContext.ProcessFlows
            .AsNoTracking()
            .Where(flow => flow.Code == code)
            .Select(flow => new ProcessFlowDto(
                flow.Id,
                flow.Code,
                flow.Name,
                flow.Kind,
                flow.ProjectStageOrder,
                flow.Stages
                    .Where(stage => all || stage.IsActive)
                    .OrderBy(stage => stage.Order)
                    .Select(stage => new ProcessStageDto(
                        stage.Id,
                        stage.Order,
                        stage.Title,
                        stage.ShortTitle,
                        stage.Steps
                            .AsQueryable()
                            .Where(step => all || step.IsActive)
                            .OrderBy(step => step.Order)
                            .Select(ProcessMappings.ToStepDto)
                            .ToList(),
                        stage.IsActive))
                    .ToList(),
                flow.UsedFor,
                flow.IsCustomized,
                Convert.ToBase64String(flow.RowVersion)))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Akış bulunamadı.");
        return flow;
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
                Flow = new ProcessFlowSummaryDto(p.ProcessFlow.Code, p.ProcessFlow.Name, p.ProcessFlow.Kind),
                p.ProcessFlow.ProjectStageOrder,
                p.Source,
                p.RequestType,
                p.RequiresOffer,
                p.Level,
                p.CustomValues,
            })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Proje bulunamadı.");
        var context = new ConditionContext(
            project.Source, project.RequestType, project.RequiresOffer, project.Level,
            CustomValueRules.ToText(project.CustomValues),
            await dbContext.CustomFields.ActiveKeysAsync(cancellationToken));

        // Active or postponed: the current step. Closed: the step where the project was closed (last record).
        var hasCurrentStep = project.CurrentStepId is not null;
        var referenceStepId = hasCurrentStep
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
                    Permission = new StepPermissionInfo(s.IsDecision, s.ResponsiblePermission, s.ApproverPermission),
                })
                .SingleOrDefaultAsync(cancellationToken);

        var currentStep = hasCurrentStep
            ? await dbContext.ProcessSteps
                .AsNoTracking()
                .Where(s => s.Id == project.CurrentStepId)
                .Select(ProcessMappings.ToStepDto)
                .SingleOrDefaultAsync(cancellationToken)
            : null;

        var stages = await dbContext.ProcessStages
            .AsNoTracking()
            .Where(stage => stage.ProcessFlowId == project.ProcessFlowId && stage.IsActive)
            .OrderBy(stage => stage.Order)
            .Select(stage => new { stage.Id, stage.Order, stage.ShortTitle, StepCount = stage.Steps.Count(s => s.IsActive) })
            .ToListAsync(cancellationToken);

        var flowSteps = await LoadFlowStepsAsync(project.ProcessFlowId, cancellationToken);
        var appliedOrders = flowSteps
            .Where(s => StepConditions.Applies(s.Conditions, context))
            .Select(s => s.Order)
            .ToList();

        // Position of the reference step among the steps done on the project; skipped steps do not count.
        int? referencePosition = referenceStep is null
            ? null
            : appliedOrders.Count(order => order < referenceStep.Order) + 1;

        var access = await dbContext.LoadAccessAsync(currentUser.UserId, cancellationToken);

        var actions = ProcessProgress.GetAvailableActions(
            project.Status,
            hasCurrentStep ? referenceStep?.Permission : null,
            access,
            // Only a request can be postponed: a record of a request flow before its project stage.
            inRequestPhase: project.Flow.Kind == ProcessFlowKind.Request
                && referenceStep is not null
                && (project.ProjectStageOrder is null || referenceStep.StageOrder < project.ProjectStageOrder));

        // The step's prerequisites, shown only to someone who may act on it (Faz 7 · A4). "No" stays possible.
        if (currentStep is not null && (actions.CanComplete || actions.CanAnswer))
        {
            var unmet = await UnmetPrerequisitesAsync(projectId, currentStep.Prerequisites, cancellationToken);
            actions = actions with { CanComplete = actions.CanComplete && unmet.Count == 0, UnmetPrerequisites = unmet };
        }

        return new ProjectProcessDto(
            project.Id,
            project.Code,
            project.Flow,
            StepConditions.SkippedStepIds(flowSteps, context),
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
            actions);
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

        await EnsurePrerequisitesAsync(project.Id, step.Id, cancellationToken);

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
        RequestDecision? closingDecision = null;

        if (answer == StepAnswer.Yes)
        {
            if (request.JumpTargetStepId is not null)
            {
                throw new BusinessRuleException("Evet cevabında dönüş adımı seçilmez.");
            }

            await EnsurePrerequisitesAsync(project.Id, step.Id, cancellationToken);

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

            // A "No" that closes a request (yeni proje 4: not suitable, 11: offer not accepted) is recorded with the
            // Talep Ret / Erteleme Formu, like a rejection. Other flows (e.g. process improvement) close without it.
            if (step.NoOutcome is not null && await IsRequestFlowAsync(project.ProcessFlowId, cancellationToken))
            {
                closingDecision = request.ClosingDecision is { } form
                    ? NewDecision(project, RequestDecisionKind.Reject, form, reevaluationDate: null)
                    : throw new BusinessRuleException(
                        "Talep kapanacağı için Talep Ret / Erteleme Formu doldurulmalıdır (karar tarihi, kararı veren, gerekçe).");
            }
            targetStep = transition.JumpedToStepId is { } jumpedTo
                ? await LoadStepRefAsync(jumpedTo, cancellationToken)
                : null;
        }

        var action = answer == StepAnswer.Yes ? StepAction.Yes : StepAction.No;
        await SaveTransitionAsync(project, step, user, action, request.Note, transition, cancellationToken, closingDecision);
        await NotifyAfterTransitionAsync(project, step, transition, targetStep, cancellationToken);

        return await GetProjectProcessAsync(projectId, cancellationToken);
    }

    public async Task<ProjectProcessDto> RejectAsync(
        int projectId, RejectProjectDto request, CancellationToken cancellationToken)
    {
        var (project, step, user) = await LoadForCommandAsync(
            projectId, request.ExpectedStepId, ProcessProgress.WhyCannotDecide, cancellationToken);

        // The record is written on the step the request was on, with the explanation as its note, and the form with it.
        var decision = NewDecision(project, RequestDecisionKind.Reject, request.Decision!, reevaluationDate: null);
        var transition = ProcessTransitions.AfterRejected();
        await SaveTransitionAsync(
            project, step, user, StepAction.Rejected, decision.Explanation, transition, cancellationToken, decision);
        await NotifyAfterTransitionAsync(project, step, transition, null, cancellationToken);

        return await GetProjectProcessAsync(projectId, cancellationToken);
    }

    public async Task<ProjectProcessDto> PostponeAsync(
        int projectId, PostponeProjectDto request, CancellationToken cancellationToken)
    {
        var (project, step, user) = await LoadForCommandAsync(
            projectId, request.ExpectedStepId, ProcessProgress.WhyCannotDecide, cancellationToken);

        // A project that has passed the request stages is rejected, not postponed (the form is for requests).
        var inRequestPhase = await dbContext.ProcessSteps
            .AsNoTracking()
            .Where(s => s.Id == step.Id)
            .Select(s => s.ProcessFlow.Kind == ProcessFlowKind.Request
                && (s.ProcessFlow.ProjectStageOrder == null || s.ProcessStage.Order < s.ProcessFlow.ProjectStageOrder))
            .SingleAsync(cancellationToken);
        if (!inRequestPhase)
        {
            throw new BusinessRuleException("Yalnızca talep aşamasındaki bir kayıt ertelenebilir; projeye dönüşmüş kayıt reddedilebilir.");
        }

        var decision = NewDecision(project, RequestDecisionKind.Postpone, request.Decision!, request.ReevaluationDate);
        var transition = ProcessTransitions.AfterPostponed(step.Id);
        await SaveTransitionAsync(
            project, step, user, StepAction.Postponed, decision.Explanation, transition, cancellationToken, decision);
        await NotifyTeamAsync(project,
            $"{project.Code} ertelendi; yeniden değerlendirme tarihi {decision.ReevaluationDate:dd.MM.yyyy}.", cancellationToken);

        return await GetProjectProcessAsync(projectId, cancellationToken);
    }

    public async Task<ProjectProcessDto> ResumeAsync(
        int projectId, ResumeProjectDto request, CancellationToken cancellationToken)
    {
        var (project, step, user) = await LoadForCommandAsync(
            projectId, request.ExpectedStepId, ProcessProgress.WhyCannotDecide, cancellationToken,
            expectedStatus: ProjectStatus.Postponed);

        var transition = ProcessTransitions.AfterResumed(step.Id);
        await SaveTransitionAsync(project, step, user, StepAction.Resumed, request.Note, transition, cancellationToken);
        await NotifyTeamAsync(project,
            $"{project.Code} yeniden değerlendirmeye alındı; {step.Code}. adımdan devam ediyor.", cancellationToken);

        return await GetProjectProcessAsync(projectId, cancellationToken);
    }

    public async Task<IReadOnlyList<RequestDecisionDto>> GetRequestDecisionsAsync(
        int projectId, CancellationToken cancellationToken)
    {
        if (!await dbContext.Projects.AnyAsync(p => p.Id == projectId, cancellationToken))
        {
            throw new NotFoundException("Proje bulunamadı.");
        }

        // Newest first. Reasons are read as a list column, so the rows are mapped after loading.
        var decisions = await dbContext.RequestDecisions
            .AsNoTracking()
            .Where(d => d.ProjectId == projectId)
            .OrderByDescending(d => d.StepRecord.PerformedAt)
            .ThenByDescending(d => d.Id)
            .Select(d => new
            {
                d.Id, d.Decision, d.DecisionDate, d.DecidedBy, d.Reasons, d.ReasonOther, d.Explanation, d.ReevaluationDate,
                StepCode = d.StepRecord.ProcessStep.Code,
                RecordedBy = new UserSummaryDto(d.StepRecord.PerformedBy.Id, d.StepRecord.PerformedBy.FullName),
                d.StepRecord.PerformedAt,
            })
            .ToListAsync(cancellationToken);

        return decisions
            .Select(d => new RequestDecisionDto(
                d.Id, d.Decision, d.DecisionDate, d.DecidedBy, d.Reasons, d.ReasonOther, d.Explanation,
                d.ReevaluationDate, d.StepCode, d.RecordedBy, d.PerformedAt))
            .ToList();
    }

    // ---- Shared by the commands (complete, answer, reject, postpone, resume) ----

    private Task<bool> IsRequestFlowAsync(int flowId, CancellationToken cancellationToken) =>
        dbContext.ProcessFlows.AnyAsync(f => f.Id == flowId && f.Kind == ProcessFlowKind.Request, cancellationToken);

    /// <summary>The filled-in Talep Ret / Erteleme Formu; required fields are guaranteed by the DTO's attributes.</summary>
    private static RequestDecision NewDecision(
        Project project, RequestDecisionKind kind, RequestDecisionFormDto form, DateOnly? reevaluationDate)
    {
        var reasons = form.Reasons!.Distinct().ToList();
        return new RequestDecision
        {
            ProjectId = project.Id,
            Decision = kind,
            DecisionDate = form.DecisionDate!.Value,
            DecidedBy = form.DecidedBy!.Trim(),
            Reasons = reasons,
            // The text belongs to "Diğer" only.
            ReasonOther = reasons.Contains(DecisionReason.Other) && !string.IsNullOrWhiteSpace(form.ReasonOther)
                ? form.ReasonOther.Trim()
                : null,
            Explanation = form.Explanation!.Trim(),
            ReevaluationDate = kind == RequestDecisionKind.Postpone ? reevaluationDate : null,
        };
    }

    private record CurrentStepInfo(
        int Id, string Code, int Order, bool IsDecision, ProjectStatus? NoOutcome, StepPermissionInfo Permission);

    /// <summary>A step the project moves to: enough to save the move and send notifications.</summary>
    private record StepRef(int Id, string Code, Permission? ApproverPermission);

    /// <summary>
    /// The next step of the project's flow whose conditions hold (StepConditions); null after the last one.
    /// Conditions are checked with the project's values as they are now.
    /// </summary>
    private async Task<StepRef?> LoadNextStepAsync(
        Project project, CurrentStepInfo step, CancellationToken cancellationToken)
    {
        var flowSteps = await LoadFlowStepsAsync(project.ProcessFlowId, cancellationToken);
        var context = new ConditionContext(
            project.Source, project.RequestType, project.RequiresOffer, project.Level,
            CustomValueRules.ToText(project.CustomValues),
            await dbContext.CustomFields.ActiveKeysAsync(cancellationToken));

        return StepConditions.NextStepId(flowSteps, step.Order, context) is { } nextStepId
            ? await LoadStepRefAsync(nextStepId, cancellationToken)
            : null;
    }

    /// <summary>Every step of the flow with its order and conditions (a flow has at most a few dozen steps).</summary>
    private async Task<IReadOnlyList<ConditionalStep>> LoadFlowStepsAsync(
        int flowId, CancellationToken cancellationToken) =>
        await dbContext.ProcessSteps.ConditionalStepsAsync(flowId, cancellationToken);

    private Task<StepRef?> LoadStepRefAsync(int stepId, CancellationToken cancellationToken) =>
        dbContext.ProcessSteps
            .AsNoTracking()
            .Where(s => s.Id == stepId)
            .Select(s => new StepRef(s.Id, s.Code, s.ApproverPermission))
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>Completing a step or answering "Yes" needs the step's prerequisites (Faz 7 · A4); 400 with the first missing one.</summary>
    private async Task EnsurePrerequisitesAsync(int projectId, int stepId, CancellationToken cancellationToken)
    {
        var prerequisites = await dbContext.ProcessSteps
            .AsNoTracking()
            .Where(s => s.Id == stepId)
            .Select(s => s.Prerequisites)
            .SingleAsync(cancellationToken);

        var unmet = await UnmetPrerequisitesAsync(projectId, prerequisites, cancellationToken);
        if (unmet.Count > 0)
        {
            throw new BusinessRuleException(StepPrerequisites.BlockedMessage(unmet));
        }
    }

    /// <summary>
    /// Collects only what the step's prerequisites ask about, then lets StepPrerequisites decide.
    /// Rules: docs/configurability.md → Adım ön koşulları.
    /// </summary>
    private async Task<IReadOnlyList<UnmetPrerequisiteDto>> UnmetPrerequisitesAsync(
        int projectId, IReadOnlyList<StepPrerequisite> prerequisites, CancellationToken cancellationToken)
    {
        if (prerequisites.Count == 0)
        {
            return [];
        }

        var kinds = prerequisites.Select(p => p.Kind).ToHashSet();
        string[] ValuesOf(PrerequisiteKind kind) => prerequisites
            .Where(p => p.Kind == kind && p.Value is not null)
            .Select(p => p.Value!)
            .Distinct()
            .ToArray();

        var requestFormComplete = false;
        var levelApplied = false;
        if (kinds.Contains(PrerequisiteKind.RequestFormComplete) || kinds.Contains(PrerequisiteKind.LevelApplied))
        {
            var project = await dbContext.Projects
                .AsNoTracking()
                .Where(p => p.Id == projectId)
                .Select(p => new
                {
                    p.Name, p.Description, p.CustomerName, p.RequesterName, p.RequestChannel, p.RequestChannelOther, p.Level,
                    p.CustomValues,
                })
                .SingleAsync(cancellationToken);

            // The same required fields as the request form (CreateProjectDto), and the company's required fields (A6).
            requestFormComplete = !string.IsNullOrWhiteSpace(project.Name)
                && !string.IsNullOrWhiteSpace(project.Description)
                && !string.IsNullOrWhiteSpace(project.CustomerName)
                && !string.IsNullOrWhiteSpace(project.RequesterName)
                && (project.RequestChannel != RequestChannel.Other || !string.IsNullOrWhiteSpace(project.RequestChannelOther))
                && CustomValueRules.RequiredFieldsFilled(
                    await dbContext.CustomFields.ActiveDefinitionsAsync(cancellationToken), project.CustomValues);
            levelApplied = project.Level is not null;
        }

        var completedForms = new HashSet<string>();
        var formNames = new Dictionary<string, string>();
        foreach (var setCode in ValuesOf(PrerequisiteKind.QuestionnaireComplete))
        {
            try
            {
                var form = await questionnaires.GetAsync(projectId, setCode, cancellationToken);
                formNames[setCode] = form.Set.Name;
                if (form.Summary.IsComplete)
                {
                    completedForms.Add(setCode);
                }
            }
            catch (NotFoundException)
            {
                // The company has no such form: the prerequisite cannot hold, and its code is shown as the name.
            }
        }

        var meetingCount = kinds.Contains(PrerequisiteKind.MeetingExists)
            ? await dbContext.Meetings.CountAsync(m => m.ProjectId == projectId, cancellationToken)
            : 0;
        var riskCount = kinds.Contains(PrerequisiteKind.RiskExists)
            ? await dbContext.Risks.CountAsync(r => r.ProjectId == projectId, cancellationToken)
            : 0;

        var requirementCount = 0;
        var unapprovedRequirementCount = 0;
        if (kinds.Contains(PrerequisiteKind.RequirementsApproved))
        {
            var statuses = await dbContext.Requirements
                .AsNoTracking()
                .Where(r => r.ProjectId == projectId)
                .Select(r => r.Status)
                .ToListAsync(cancellationToken);
            requirementCount = statuses.Count;
            unapprovedRequirementCount = statuses.Count(s => s is RequirementStatus.Draft or RequirementStatus.InReview);
        }

        var approvedDocuments = new HashSet<string>();
        var documentNames = new Dictionary<string, string>();
        var documentCodes = ValuesOf(PrerequisiteKind.DocumentApproved);
        if (documentCodes.Length > 0)
        {
            var types = await dbContext.DocumentTypes
                .AsNoTracking()
                .Where(t => documentCodes.Contains(t.Code))
                .Select(t => new { t.Code, t.Name })
                .ToListAsync(cancellationToken);
            foreach (var type in types)
            {
                documentNames[type.Code] = type.Name;
            }

            approvedDocuments.UnionWith(await dbContext.ProjectDocuments
                .AsNoTracking()
                .Where(d => d.ProjectId == projectId
                    && documentCodes.Contains(d.DocumentType.Code)
                    && (d.Status == DocumentStatus.Approved || d.Status == DocumentStatus.NotRequired))
                .Select(d => d.DocumentType.Code)
                .ToListAsync(cancellationToken));
        }

        var facts = new PrerequisiteFacts(
            requestFormComplete, completedForms, levelApplied, meetingCount, riskCount,
            requirementCount, unapprovedRequirementCount, approvedDocuments, formNames, documentNames);
        return StepPrerequisites.Evaluate(prerequisites, facts, projectId);
    }

    /// <summary>
    /// Loads the project (tracked, so RowVersion is checked on save), its current step and the acting user,
    /// and runs the checks every command shares, in this order: 404, 409 not active, 409 step changed, 403 role.
    /// whyForbidden is the command's own permission rule (ProcessProgress.WhyCannot...): null means allowed.
    /// </summary>
    private async Task<(Project Project, CurrentStepInfo Step, UserAccess User)> LoadForCommandAsync(
        int projectId, int expectedStepId, Func<StepPermissionInfo, UserAccess, string?> whyForbidden,
        CancellationToken cancellationToken, ProjectStatus expectedStatus = ProjectStatus.Active)
    {
        var project = await dbContext.Projects.SingleOrDefaultAsync(p => p.Id == projectId, cancellationToken)
            ?? throw new NotFoundException("Proje bulunamadı.");

        if (project.Status != expectedStatus || project.CurrentStepId is null)
        {
            throw new ConflictException(expectedStatus == ProjectStatus.Postponed
                ? "Talep ertelenmiş değil; değerlendirmeye geri alınacak bir şey yok."
                : project.Status == ProjectStatus.Postponed
                    ? "Talep ertelendi; önce değerlendirmeye geri alınmalıdır."
                    : "Proje aktif değil; süreçte işlem yapılamaz.");
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
                new StepPermissionInfo(s.IsDecision, s.ResponsiblePermission, s.ApproverPermission)))
            .SingleAsync(cancellationToken);

        var user = await dbContext.LoadAccessAsync(currentUser.UserId, cancellationToken);

        if (user is null)
        {
            throw new ForbiddenException("İşlem yapmak için giriş yapılmalıdır.");
        }

        if (whyForbidden(step.Permission, user) is { } reason)
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
        Project project, CurrentStepInfo step, UserAccess user, StepAction action, string? note,
        ProcessTransition transition, CancellationToken cancellationToken, RequestDecision? decision = null)
    {
        var record = new StepRecord
        {
            ProjectId = project.Id,
            ProcessStepId = step.Id,
            Action = action,
            JumpedToStepId = transition.JumpedToStepId,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            PerformedById = user.UserId,
            PerformedAt = timeProvider.GetUtcNow().UtcDateTime,
        };
        dbContext.StepRecords.Add(record);

        if (decision is not null)
        {
            decision.StepRecord = record;
            dbContext.RequestDecisions.Add(decision);
        }

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

        if (toStep.ApproverPermission is { } approver)
        {
            await notifications.NotifyPermissionAsync(
                approver, project.Id, $"{project.Code} · {toStep.Code} onayınızı bekliyor.", cancellationToken);
        }
    }

    private Task NotifyClosedAsync(Project project, CancellationToken cancellationToken) =>
        NotifyTeamAsync(project, $"{project.Code} kapandı: {project.Status.ToDisplayName()}.", cancellationToken);

    /// <summary>Tells the project's analyst and project manager (not the person who acted; NotificationService skips them).</summary>
    private async Task NotifyTeamAsync(Project project, string message, CancellationToken cancellationToken)
    {
        var userIds = new[] { project.BusinessAnalystId, project.ProjectManagerId }
            .OfType<int>()
            .Distinct()
            .ToList();

        if (userIds.Count > 0)
        {
            await notifications.NotifyUsersAsync(userIds, project.Id, message, cancellationToken);
        }
    }
}
