using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Common.Models;
using Isdlc.Application.Dashboard.Dtos;
using Isdlc.Application.OpenIssues.Dtos;
using Isdlc.Domain.Identity;
using Isdlc.Domain.OpenIssues;
using Isdlc.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace Isdlc.Application.Dashboard;

public class DashboardService(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUser,
    TimeProvider timeProvider) : IDashboardService
{
    /// <summary>Each list shows its first rows; the rest is on the module's own page.</summary>
    public const int ListSize = 10;

    /// <summary>Stages 1–3 are the request phase ("Talepler"), the same rule as the Projects list.</summary>
    private const int LastRequestStageOrder = 3;

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

        // The general sections count these projects.
        var projects = dbContext.Projects.AsNoTracking();
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

        var summary = new DashboardSummaryDto(
            WaitingRequests: await projects.CountAsync(p => p.Status == ProjectStatus.Active
                && p.CurrentStep != null && p.CurrentStep.ProcessStage.Order <= LastRequestStageOrder, cancellationToken),
            ActiveProjects: await projects.CountAsync(p => p.Status == ProjectStatus.Active
                && p.CurrentStep != null && p.CurrentStep.ProcessStage.Order > LastRequestStageOrder, cancellationToken),
            MyWaitingSteps: myWaitingSteps.TotalCount,
            OverdueOpenIssues: overdueOpenIssues.TotalCount,
            OverdueProjects: await projects.CountAsync(p => p.Status == ProjectStatus.Active
                && p.DueDate != null && p.DueDate < today, cancellationToken),
            FinishedThisYear: await FinishedThisYearAsync(projects, now, cancellationToken));

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
            await RecentActivityAsync(projects, cancellationToken));
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

    /// <summary>Active records by the stage of their current step; all 13 stages, empty ones too.</summary>
    private async Task<IReadOnlyList<StageCountDto>> StagesAsync(
        IQueryable<Project> projects, CancellationToken cancellationToken)
    {
        var counts = await projects
            .Where(p => p.Status == ProjectStatus.Active && p.CurrentStep != null)
            .GroupBy(p => p.CurrentStep!.ProcessStage.Order)
            .Select(g => new { Order = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Order, x => x.Count, cancellationToken);

        var stages = await dbContext.ProcessStages
            .AsNoTracking()
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
