using Isdlc.Application.Assessment;
using Isdlc.Application.Assessment.Dtos;
using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Common.Models;
using Isdlc.Application.Dashboard.Dtos;
using Isdlc.Application.OpenIssues.Dtos;
using Isdlc.Application.Risks;
using Isdlc.Domain.Identity;
using Isdlc.Domain.OpenIssues;
using Isdlc.Domain.Process;
using Isdlc.Domain.Projects;
using Isdlc.Domain.Risks;
using Microsoft.EntityFrameworkCore;

namespace Isdlc.Application.Dashboard;

public class DashboardService(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUser,
    TimeProvider timeProvider) : IDashboardService
{
    /// <summary>Each list shows its first rows; the rest is on the module's own page.</summary>
    public const int ListSize = 10;

    /// <summary>Acceptance: scoring at 3.1, the department manager's decision at 3.2 (docs/dashboard-api.md, decision 13).</summary>
    private static readonly string[] AcceptanceStepCodes = ["3.1", "3.2"];

    public async Task<DashboardDto> GetAsync(DashboardScope scope, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new ForbiddenException("İşlem yapmak için giriş yapılmalıdır.");
        var role = await dbContext.Users
            .Where(u => u.Id == userId)
            .Select(u => (UserRole?)u.Role)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ForbiddenException("İşlem yapmak için giriş yapılmalıdır.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(now);

        // The general sections count these projects: requests and projects only (not improvement, planning or billing).
        var projects = dbContext.Projects.AsNoTracking().Where(p => p.ProcessFlow.Kind == ProcessFlowKind.Request);
        if (scope == DashboardScope.Mine)
        {
            projects = projects.Where(p => p.BusinessAnalystId == userId || p.ProjectManagerId == userId);
        }

        var myWaitingSteps = await MyWaitingStepsAsync(userId, role, now, cancellationToken);
        var overdueOpenIssues = await OpenIssueListAsync(
            dbContext.OpenIssues.Where(x =>
                projects.Any(p => p.Id == x.ProjectId && p.Status == ProjectStatus.Active)
                && x.Status != OpenIssueStatus.Closed && x.DueDate != null && x.DueDate < today),
            today, cancellationToken);
        var highRisks = await HighRisksAsync(projects, cancellationToken);

        var summary = new DashboardSummaryDto(
            // The same request / project rule as the Projects list (docs/process-api.md → "Talep mi, proje mi").
            WaitingRequests: await projects.CountAsync(p => p.Status == ProjectStatus.Active
                && p.CurrentStep != null
                && (p.ProcessFlow.ProjectStageOrder == null
                    || p.CurrentStep.ProcessStage.Order < p.ProcessFlow.ProjectStageOrder), cancellationToken),
            ActiveProjects: await projects.CountAsync(p => p.Status == ProjectStatus.Active
                && p.CurrentStep != null
                && p.ProcessFlow.ProjectStageOrder != null
                && p.CurrentStep.ProcessStage.Order >= p.ProcessFlow.ProjectStageOrder, cancellationToken),
            MyWaitingSteps: myWaitingSteps.TotalCount,
            OverdueOpenIssues: overdueOpenIssues.TotalCount,
            OverdueProjects: await projects.CountAsync(p => p.Status == ProjectStatus.Active
                && p.DueDate != null && p.DueDate < today, cancellationToken),
            FinishedThisYear: await FinishedThisYearAsync(projects, now, cancellationToken),
            HighRisks: highRisks.TotalCount);

        return new DashboardDto(
            scope,
            summary,
            await StagesAsync(projects, cancellationToken),
            myWaitingSteps,
            await OpenIssueListAsync(
                dbContext.OpenIssues.Where(x => x.OwnerUserId == userId && x.Status != OpenIssueStatus.Closed
                    && x.Project.Status == ProjectStatus.Active),
                today, cancellationToken),
            overdueOpenIssues,
            await RecentActivityAsync(projects, cancellationToken),
            highRisks,
            await LevelsAsync(projects, cancellationToken),
            await AcceptanceQueueAsync(projects, cancellationToken));
    }

    /// <summary>
    /// Risks that are not closed and whose score is high, on active projects (docs/dashboard-api.md, decisions 10–11).
    /// The threshold comes from RiskRules, so the dashboard and the Risks tab always agree. Highest score first.
    /// </summary>
    private async Task<DashboardListDto<HighRiskDto>> HighRisksAsync(
        IQueryable<Project> projects, CancellationToken cancellationToken)
    {
        var highMin = RiskRules.ScoreRange(RiskSeverity.High).Min;

        var risks = dbContext.Risks
            .AsNoTracking()
            .Where(r => projects.Any(p => p.Id == r.ProjectId && p.Status == ProjectStatus.Active)
                && r.Status != RiskStatus.Closed
                && r.Probability * r.Impact >= highMin);

        var totalCount = await risks.CountAsync(cancellationToken);
        var items = await risks
            .OrderByDescending(r => r.Probability * r.Impact)
            .ThenBy(r => r.Project.Code)
            .ThenBy(r => r.Code.Length)
            .ThenBy(r => r.Code)
            .Take(ListSize)
            .Select(r => new HighRiskDto(
                new DashboardProjectDto(r.Project.Id, r.Project.Code, r.Project.Name),
                r.Id,
                r.Code,
                r.Title,
                r.Probability,
                r.Impact,
                r.Probability * r.Impact,
                r.Status,
                new UserSummaryDto(r.OwnerUser.Id, r.OwnerUser.FullName)))
            .ToListAsync(cancellationToken);

        return new DashboardListDto<HighRiskDto>(totalCount, items);
    }

    /// <summary>Active records by applied level; a record without a level is "not set" (decision 12).</summary>
    private static async Task<LevelCountsDto> LevelsAsync(IQueryable<Project> projects, CancellationToken cancellationToken)
    {
        var counts = await projects
            .Where(p => p.Status == ProjectStatus.Active && p.CurrentStep != null)
            .GroupBy(p => p.Level)
            .Select(g => new { Level = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        int CountOf(int? level) => counts.SingleOrDefault(c => c.Level == level)?.Count ?? 0;

        // A level outside 1–3 cannot be applied (Assessment); if one ever appears it is not counted as "not set".
        return new LevelCountsDto(CountOf(1), CountOf(2), CountOf(3), CountOf(null));
    }

    /// <summary>
    /// Active requests at step 3.1 or 3.2 with their acceptance scoring (decision 13). The weighted score and the
    /// recommendation come from AssessmentRules, so they match the Seviye &amp; Kabul tab. Longest waiting first.
    /// </summary>
    private async Task<DashboardListDto<AcceptanceQueueItemDto>> AcceptanceQueueAsync(
        IQueryable<Project> projects, CancellationToken cancellationToken)
    {
        var requests = projects.Where(p => p.Status == ProjectStatus.Active
            && p.CurrentStep != null && AcceptanceStepCodes.Contains(p.CurrentStep.Code));

        var totalCount = await requests.CountAsync(cancellationToken);

        // Sorted before Select: EF cannot sort on the projected DTOs.
        var rows = await requests
            .Select(p => new
            {
                Project = p,
                WaitingSince = p.StepRecords.Max(r => (DateTime?)r.PerformedAt) ?? p.CreatedAt,
            })
            .OrderBy(x => x.WaitingSince)
            .ThenBy(x => x.Project.Code)
            .Take(ListSize)
            .Select(x => new
            {
                Project = new DashboardProjectDto(x.Project.Id, x.Project.Code, x.Project.Name),
                StepCode = x.Project.CurrentStep!.Code,
                x.WaitingSince,
                Scores = dbContext.ProjectAssessments
                    .Where(a => a.ProjectId == x.Project.Id)
                    .Select(a => new AcceptanceScoresDto
                    {
                        StrategicFit = a.StrategicFit,
                        CommercialValue = a.CommercialValue,
                        TechnicalFeasibility = a.TechnicalFeasibility,
                        ResourceAndSchedule = a.ResourceAndSchedule,
                        CustomerFit = a.CustomerFit,
                        RiskLevel = a.RiskLevel,
                    })
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(x =>
        {
            // No assessment yet: nothing is scored.
            var scores = x.Scores ?? new AcceptanceScoresDto();
            var weighted = AssessmentRules.WeightedScore(scores);
            return new AcceptanceQueueItemDto(
                x.Project,
                x.StepCode,
                weighted,
                weighted is { } score ? AssessmentRules.RecommendationOf(score) : null,
                AssessmentRules.AnsweredCount(scores),
                x.WaitingSince);
        }).ToList();

        return new DashboardListDto<AcceptanceQueueItemDto>(totalCount, items);
    }

    /// <summary>
    /// Active projects whose current step this user can act on: the step's approver role, or its responsible role
    /// when it has no approver (ProcessProgress.CanActOnStep, written so the database can run it).
    /// An analyst or project manager sees only the projects they are assigned to, or nobody is (docs/dashboard-api.md, decision 3).
    /// Longest waiting first.
    /// </summary>
    private async Task<DashboardListDto<WaitingStepDto>> MyWaitingStepsAsync(
        int userId, UserRole role, DateTime now, CancellationToken cancellationToken)
    {
        var query = dbContext.Projects
            .AsNoTracking()
            .Where(p => p.Status == ProjectStatus.Active && p.CurrentStep != null
                && (p.CurrentStep.ApproverRole == role
                    || (p.CurrentStep.ApproverRole == null && p.CurrentStep.ResponsibleRole == role)));

        if (role == UserRole.BusinessAnalyst)
        {
            query = query.Where(p => p.BusinessAnalystId == null || p.BusinessAnalystId == userId);
        }
        else if (role == UserRole.ProjectManager)
        {
            query = query.Where(p => p.ProjectManagerId == null || p.ProjectManagerId == userId);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        // Sorted before Select: EF cannot sort on the projected DTOs.
        var items = await query
            .Select(p => new
            {
                Project = p,
                // Since the last process record; a new request waits since it was opened.
                WaitingSince = p.StepRecords.Max(r => (DateTime?)r.PerformedAt) ?? p.CreatedAt,
            })
            .OrderBy(x => x.WaitingSince)
            .ThenBy(x => x.Project.Code)
            .Take(ListSize)
            .Select(x => new
            {
                Project = new DashboardProjectDto(x.Project.Id, x.Project.Code, x.Project.Name),
                Step = new WaitingStepRefDto(
                    x.Project.CurrentStep!.Id,
                    x.Project.CurrentStep.Code,
                    x.Project.CurrentStep.ProcessStage.Order,
                    x.Project.CurrentStep.ProcessStage.ShortTitle),
                IsApproval = x.Project.CurrentStep.ApproverRole != null,
                x.Project.CurrentStep.IsDecision,
                x.WaitingSince,
            })
            .ToListAsync(cancellationToken);

        var today = DateOnly.FromDateTime(now);
        return new DashboardListDto<WaitingStepDto>(
            totalCount,
            items.Select(x => new WaitingStepDto(
                    x.Project, x.Step, x.IsApproval, x.IsDecision, x.WaitingSince,
                    Math.Max(0, today.DayNumber - DateOnly.FromDateTime(x.WaitingSince).DayNumber)))
                .ToList());
    }

    /// <summary>Overdue first, then by due date (none last), then by code.</summary>
    private static async Task<DashboardListDto<DashboardOpenIssueDto>> OpenIssueListAsync(
        IQueryable<OpenIssue> issues, DateOnly today, CancellationToken cancellationToken)
    {
        var totalCount = await issues.CountAsync(cancellationToken);
        var items = await issues
            .AsNoTracking()
            .OrderBy(x => x.DueDate != null && x.DueDate < today ? 0 : 1)
            .ThenBy(x => x.DueDate == null ? 1 : 0)
            .ThenBy(x => x.DueDate)
            .ThenBy(x => x.Project.Code)
            .ThenBy(x => x.Code.Length)
            .ThenBy(x => x.Code)
            .Take(ListSize)
            .Select(x => new DashboardOpenIssueDto(
                new DashboardProjectDto(x.Project.Id, x.Project.Code, x.Project.Name),
                x.Id,
                x.Code,
                x.Type,
                x.Title,
                new OwnerDto(x.OwnerUserId, x.OwnerUser != null ? x.OwnerUser.FullName : x.OwnerName!),
                x.DueDate,
                x.Status,
                x.Status != OpenIssueStatus.Closed && x.DueDate != null && x.DueDate < today))
            .ToListAsync(cancellationToken);

        return new DashboardListDto<DashboardOpenIssueDto>(totalCount, items);
    }

    /// <summary>
    /// Records that are no longer active and finished this year. A project has no closing date of its own:
    /// its last process record is used (docs/dashboard-api.md, decision 5).
    /// </summary>
    private static async Task<FinishedThisYearDto> FinishedThisYearAsync(
        IQueryable<Project> projects, DateTime now, CancellationToken cancellationToken)
    {
        var yearStart = new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var counts = await projects
            .Where(p => p.Status != ProjectStatus.Active)
            .Select(p => new
            {
                p.Status,
                FinishedAt = p.StepRecords.Max(r => (DateTime?)r.PerformedAt) ?? p.UpdatedAt ?? p.CreatedAt,
            })
            .Where(x => x.FinishedAt >= yearStart)
            .GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, cancellationToken);

        return new FinishedThisYearDto(
            counts.GetValueOrDefault(ProjectStatus.Closed),
            counts.GetValueOrDefault(ProjectStatus.Lost),
            counts.GetValueOrDefault(ProjectStatus.Rejected),
            counts.GetValueOrDefault(ProjectStatus.Redirected));
    }

    /// <summary>
    /// Active records of the new-request flow by the stage of their current step; all its stages, empty ones too.
    /// Stage orders repeat across flows, so only one flow is counted (Faz 4, D9 decides how other flows are shown).
    /// </summary>
    private async Task<IReadOnlyList<StageCountDto>> StagesAsync(
        IQueryable<Project> projects, CancellationToken cancellationToken)
    {
        var counts = await projects
            .Where(p => p.Status == ProjectStatus.Active && p.CurrentStep != null
                && p.ProcessFlow.Code == ProcessFlowCodes.NewRequest)
            .GroupBy(p => p.CurrentStep!.ProcessStage.Order)
            .Select(g => new { Order = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Order, x => x.Count, cancellationToken);

        var stages = await dbContext.ProcessStages
            .AsNoTracking()
            .Where(s => s.ProcessFlow.Code == ProcessFlowCodes.NewRequest)
            .OrderBy(s => s.Order)
            .Select(s => new { s.Order, s.ShortTitle })
            .ToListAsync(cancellationToken);

        return stages.Select(s => new StageCountDto(s.Order, s.ShortTitle, counts.GetValueOrDefault(s.Order))).ToList();
    }

    /// <summary>The latest process records of the counted projects, newest first.</summary>
    private async Task<IReadOnlyList<RecentActivityDto>> RecentActivityAsync(
        IQueryable<Project> projects, CancellationToken cancellationToken) =>
        await dbContext.StepRecords
            .AsNoTracking()
            .Where(r => projects.Any(p => p.Id == r.ProjectId))
            .OrderByDescending(r => r.PerformedAt)
            .ThenByDescending(r => r.Id)
            .Take(ListSize)
            .Select(r => new RecentActivityDto(
                new DashboardProjectDto(r.Project.Id, r.Project.Code, r.Project.Name),
                r.ProcessStep.Code,
                r.Action,
                new UserSummaryDto(r.PerformedBy.Id, r.PerformedBy.FullName),
                r.PerformedAt))
            .ToListAsync(cancellationToken);
}
