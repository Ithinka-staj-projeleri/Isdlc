using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Dashboard.Dtos;
using Isdlc.Domain.OpenIssues;
using Isdlc.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace Isdlc.Application.Dashboard;

/// <summary>
/// Reads one small row per active user, not-closed open issue and waiting step, and lets TeamWorkloadRules count them.
/// Only active records count: a closed or postponed record waits for nobody.
/// </summary>
public class TeamWorkloadService(IApplicationDbContext dbContext, TimeProvider timeProvider) : ITeamWorkloadService
{
    public async Task<IReadOnlyList<TeamMemberWorkloadDto>> GetAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        var users = await dbContext.Users
            .AsNoTracking()
            .Where(u => u.IsActive)
            .Select(u => new WorkloadUserRow(
                u.Id, u.FullName, u.Permissions.OrderBy(p => p.Permission).Select(p => p.Permission).ToList()))
            .ToListAsync(cancellationToken);

        var openIssues = await dbContext.OpenIssues
            .AsNoTracking()
            .Where(x => x.OwnerUserId != null
                && x.Status != OpenIssueStatus.Closed
                && x.Project.Status == ProjectStatus.Active)
            .Select(x => new WorkloadOpenIssueRow(x.OwnerUserId!.Value, x.Status, x.DueDate))
            .ToListAsync(cancellationToken);

        var steps = await dbContext.Projects
            .AsNoTracking()
            .Where(p => p.Status == ProjectStatus.Active && p.CurrentStep != null)
            .Select(p => new WorkloadStepRow(
                p.CurrentStep!.ResponsiblePermission, p.CurrentStep.ApproverPermission, p.BusinessAnalystId, p.ProjectManagerId))
            .ToListAsync(cancellationToken);

        return TeamWorkloadRules.Compute(users, openIssues, steps, today);
    }
}
