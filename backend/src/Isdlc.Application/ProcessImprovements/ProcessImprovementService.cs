using Isdlc.Application.Common;
using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Common.Models;
using Isdlc.Application.Notifications;
using Isdlc.Application.OpenIssues;
using Isdlc.Application.Process;
using Isdlc.Application.ProcessImprovements.Dtos;
using Isdlc.Domain.Process;
using Isdlc.Domain.ProcessImprovements;
using Isdlc.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace Isdlc.Application.ProcessImprovements;

public class ProcessImprovementService(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUser,
    INotificationService notifications,
    TimeProvider timeProvider) : IProcessImprovementService
{
    private const int MaxCodeAttempts = 3;
    private const string NotFoundMessage = "Süreç iyileştirme önerisi bulunamadı.";

    public async Task<PagedResult<ImprovementListItemDto>> ListAsync(
        ImprovementFilter filter, PageRequest pageRequest, CancellationToken cancellationToken)
    {
        var query = Proposals().AsNoTracking();
        if (filter.Status is { } status)
        {
            query = query.Where(x => x.Project.Status == status);
        }
        if (filter.StageOrder is { } stageOrder)
        {
            query = query.Where(x => x.Project.CurrentStep != null && x.Project.CurrentStep.ProcessStage.Order == stageOrder);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(x => x.Project.CreatedAt)
            .ThenByDescending(x => x.Id)
            .ApplyPaging(pageRequest)
            .Select(x => new
            {
                x.ProjectId,
                x.Project.Code,
                x.Project.Name,
                x.Source,
                x.AffectedProcess,
                x.Impact,
                x.Frequency,
                x.Ease,
                x.Project.Status,
                Step = x.Project.CurrentStep == null
                    ? null
                    : new ImprovementStepDto(
                        x.Project.CurrentStep.Id, x.Project.CurrentStep.Code,
                        x.Project.CurrentStep.ProcessStage.Order, x.Project.CurrentStep.ProcessStage.ShortTitle),
                Owner = x.Project.BusinessAnalyst == null
                    ? null
                    : new UserSummaryDto(x.Project.BusinessAnalyst.Id, x.Project.BusinessAnalyst.FullName),
                x.Project.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(x =>
            {
                var score = ImprovementRules.ScoreOf(x.Impact, x.Frequency, x.Ease);
                return new ImprovementListItemDto(
                    x.ProjectId, x.Code, x.Name, x.Source, x.AffectedProcess, ImprovementRules.PriorityOf(score), score,
                    x.Status, x.Step, x.Owner, x.CreatedAt);
            })
            .ToList();

        return new PagedResult<ImprovementListItemDto>(items, totalCount);
    }

    public async Task<ImprovementDto> GetAsync(int id, CancellationToken cancellationToken)
    {
        var x = await Proposals()
            .AsNoTracking()
            .Where(x => x.ProjectId == id)
            .Select(x => new
            {
                Row = x,
                x.Project.Code,
                x.Project.Name,
                x.Project.Description,
                x.Project.Status,
                Step = x.Project.CurrentStep == null
                    ? null
                    : new ImprovementStepDto(
                        x.Project.CurrentStep.Id, x.Project.CurrentStep.Code,
                        x.Project.CurrentStep.ProcessStage.Order, x.Project.CurrentStep.ProcessStage.ShortTitle),
                Owner = x.Project.BusinessAnalyst == null
                    ? null
                    : new UserSummaryDto(x.Project.BusinessAnalyst.Id, x.Project.BusinessAnalyst.FullName),
                CreatedBy = dbContext.Users
                    .Where(u => u.Id == x.Project.CreatedById)
                    .Select(u => new UserSummaryDto(u.Id, u.FullName))
                    .First(),
                x.Project.CreatedAt,
                // The proposal changed when either its record or its sections did.
                UpdatedAt = x.UpdatedAt > x.Project.UpdatedAt || x.Project.UpdatedAt == null ? x.UpdatedAt : x.Project.UpdatedAt,
            })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(NotFoundMessage);

        var r = x.Row;
        var score = ImprovementRules.ScoreOf(r.Impact, r.Frequency, r.Ease);
        return new ImprovementDto(
            r.ProjectId,
            x.Code,
            x.Status,
            x.Step,
            x.Name,
            x.Description,
            r.Source,
            r.AffectedProcess,
            r.ProposedSolution,
            r.SuggestedBy,
            x.Owner,
            new ImprovementPrioritizationDto(r.Impact, r.Frequency, r.Ease, score, ImprovementRules.PriorityOf(score)),
            new ImprovementStudyDto(r.CurrentState, r.RootCause, r.Solution, r.PilotPlan, r.PilotTarget, r.CostBenefit),
            new PilotEvaluationDto(r.PilotActual, r.PilotFeedback, r.PilotResult, r.PilotDecision),
            x.CreatedBy,
            x.CreatedAt,
            x.UpdatedAt);
    }

    public async Task<ImprovementDto> CreateAsync(CreateImprovementDto request, CancellationToken cancellationToken)
    {
        RequireSignedIn();
        await CheckOwnerAsync(request.OwnerUserId!.Value, cancellationToken);

        // Like a new request: the proposal starts Active at the first step of its flow.
        var flowId = await dbContext.ProcessFlows
            .Where(f => f.Code == ProcessFlowCodes.ProcessImprovement)
            .Select(f => f.Id)
            .SingleAsync(cancellationToken);
        var flowSteps = await dbContext.ProcessSteps
            .Where(s => s.ProcessFlowId == flowId)
            .Select(s => new ConditionalStep(
                s.Id, s.Order, new StepCondition(s.RequiresExternalRequest, s.RequiresOffer, s.MinLevel)))
            .ToListAsync(cancellationToken);

        // The request fields of Project mean nothing here; an internal source keeps every step of the flow.
        var project = new Project
        {
            Status = ProjectStatus.Active,
            ProcessFlowId = flowId,
            Source = RequestSource.Internal,
            RequestDate = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime),
        };
        project.CurrentStepId = StepConditions.FirstStepId(flowSteps, new ConditionContext(project.Source, false, null))
            ?? throw new InvalidOperationException($"Flow {ProcessFlowCodes.ProcessImprovement} has no steps.");

        var proposal = new ProcessImprovement { Project = project };
        ApplyProposal(proposal, request);
        dbContext.Projects.Add(project);
        dbContext.ProcessImprovements.Add(proposal);

        await SaveWithNewCodeAsync(project, flowId, cancellationToken);
        await NotifyOwnerAsync(project, previousOwnerId: null, cancellationToken);

        return await GetAsync(project.Id, cancellationToken);
    }

    public async Task<ImprovementDto> UpdateAsync(int id, UpdateImprovementDto request, CancellationToken cancellationToken)
    {
        RequireSignedIn();
        var proposal = await LoadWritableAsync(id, cancellationToken);
        await CheckOwnerAsync(request.OwnerUserId!.Value, cancellationToken);

        var previousOwnerId = proposal.Project.BusinessAnalystId;
        ApplyProposal(proposal, request);

        // A save at the same moment as another change to the record fails on RowVersion and returns 409.
        await dbContext.SaveChangesAsync(cancellationToken);
        await NotifyOwnerAsync(proposal.Project, previousOwnerId, cancellationToken);

        return await GetAsync(id, cancellationToken);
    }

    public Task<ImprovementDto> SavePrioritizationAsync(
        int id, SavePrioritizationDto request, CancellationToken cancellationToken) =>
        SaveSectionAsync(id, proposal =>
        {
            ImprovementRules.CheckScores(request.Impact, request.Frequency, request.Ease);
            proposal.Impact = request.Impact;
            proposal.Frequency = request.Frequency;
            proposal.Ease = request.Ease;
        }, cancellationToken);

    public Task<ImprovementDto> SaveStudyAsync(int id, SaveStudyDto request, CancellationToken cancellationToken) =>
        SaveSectionAsync(id, proposal =>
        {
            proposal.CurrentState = OpenIssueRules.Clean(request.CurrentState);
            proposal.RootCause = OpenIssueRules.Clean(request.RootCause);
            proposal.Solution = OpenIssueRules.Clean(request.Solution);
            proposal.PilotPlan = OpenIssueRules.Clean(request.PilotPlan);
            proposal.PilotTarget = OpenIssueRules.Clean(request.PilotTarget);
            proposal.CostBenefit = OpenIssueRules.Clean(request.CostBenefit);
        }, cancellationToken);

    public Task<ImprovementDto> SavePilotEvaluationAsync(
        int id, SavePilotEvaluationDto request, CancellationToken cancellationToken) =>
        SaveSectionAsync(id, proposal =>
        {
            proposal.PilotActual = OpenIssueRules.Clean(request.PilotActual);
            proposal.PilotFeedback = OpenIssueRules.Clean(request.PilotFeedback);
            proposal.PilotResult = request.PilotResult;
            proposal.PilotDecision = OpenIssueRules.Clean(request.PilotDecision);
        }, cancellationToken);

    // ---- Helpers ----

    /// <summary>Proposals only: an id of a record on another flow is not found.</summary>
    private IQueryable<ProcessImprovement> Proposals() =>
        dbContext.ProcessImprovements.Where(x => x.Project.ProcessFlow.Code == ProcessFlowCodes.ProcessImprovement);

    private int RequireSignedIn() =>
        currentUser.UserId ?? throw new ForbiddenException("İşlem yapmak için giriş yapılmalıdır.");

    /// <summary>404 when there is no such proposal, 409 when it is closed or rejected.</summary>
    private async Task<ProcessImprovement> LoadWritableAsync(int id, CancellationToken cancellationToken)
    {
        var proposal = await Proposals()
            .Include(x => x.Project)
            .SingleOrDefaultAsync(x => x.ProjectId == id, cancellationToken)
            ?? throw new NotFoundException(NotFoundMessage);

        if (proposal.Project.Status != ProjectStatus.Active)
        {
            throw new ConflictException("Öneri aktif değil; değiştirilemez.");
        }

        return proposal;
    }

    /// <summary>One section is saved on its own; the other sections keep their values.</summary>
    private async Task<ImprovementDto> SaveSectionAsync(
        int id, Action<ProcessImprovement> apply, CancellationToken cancellationToken)
    {
        RequireSignedIn();
        var proposal = await LoadWritableAsync(id, cancellationToken);
        apply(proposal);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    private async Task CheckOwnerAsync(int ownerUserId, CancellationToken cancellationToken)
    {
        if (!await dbContext.Users.AnyAsync(u => u.Id == ownerUserId && u.IsActive, cancellationToken))
        {
            throw new BusinessRuleException("Seçilen kişi aktif bir kullanıcı değil.");
        }
    }

    /// <summary>Section 1; required values are guaranteed by the DTO's attributes.</summary>
    private static void ApplyProposal(ProcessImprovement proposal, UpdateImprovementDto request)
    {
        proposal.Project.Name = request.Title!.Trim();
        proposal.Project.Description = request.Problem!.Trim();
        proposal.Project.BusinessAnalystId = request.OwnerUserId!.Value;
        proposal.Source = request.Source!.Value;
        proposal.AffectedProcess = request.AffectedProcess!.Trim();
        proposal.ProposedSolution = OpenIssueRules.Clean(request.ProposedSolution);
        proposal.SuggestedBy = request.SuggestedBy!.Trim();
    }

    /// <summary>
    /// "SI-001", "SI-002"… across all proposals. A proposal saved at the same moment can take the same number;
    /// the unique index stops one of them, which tries again with the next number.
    /// </summary>
    private async Task SaveWithNewCodeAsync(Project project, int flowId, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            var next = await dbContext.Projects
                .Where(p => p.ProcessFlowId == flowId && p.Code.StartsWith(ImprovementRules.CodePrefix + "-"))
                .Select(p => p.Code)
                .NextNumberAsync(cancellationToken);
            project.Code = OpenIssueRules.FormatCode(ImprovementRules.CodePrefix, next);

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateException)
            {
                var code = project.Code;
                if (!await dbContext.Projects.AsNoTracking().AnyAsync(p => p.Code == code, cancellationToken))
                {
                    throw; // Not a code clash: let the real error through.
                }
                if (attempt == MaxCodeAttempts)
                {
                    throw new ConflictException("Kayıt numarası çakıştı. Lütfen tekrar deneyin.");
                }
            }
        }
    }

    /// <summary>Tells the new owner (not when they made the change themselves; the service skips that).</summary>
    private Task NotifyOwnerAsync(Project project, int? previousOwnerId, CancellationToken cancellationToken)
    {
        if (project.BusinessAnalystId is not { } ownerId || ownerId == previousOwnerId)
        {
            return Task.CompletedTask;
        }

        return notifications.NotifyUsersAsync(
            [ownerId], project.Id, $"{project.Code} süreç iyileştirme önerisi size atandı: {project.Name}", cancellationToken);
    }
}
