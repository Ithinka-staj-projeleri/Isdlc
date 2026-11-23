using Isdlc.Application.Common;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Projects.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Isdlc.Application.Projects;

/// <summary>
/// Reads one small row per record of the project and lets ProjectSummaryRules count them: a project has at most a few
/// hundred records, and the rules stay testable without a database.
/// </summary>
public class ProjectSummaryService(IApplicationDbContext dbContext, TimeProvider timeProvider) : IProjectSummaryService
{
    public async Task<ProjectSummaryDto> GetAsync(int projectId, CancellationToken cancellationToken)
    {
        await dbContext.GetProjectAsync(projectId, cancellationToken); // 404
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        var issues = await dbContext.OpenIssues
            .AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .Select(x => new SummaryOpenIssueRow(x.Type, x.Status, x.DueDate, x.ClosedAt, x.OwnerUserId, x.OwnerName))
            .ToListAsync(cancellationToken);

        var ownerIds = issues.Select(i => i.OwnerUserId).OfType<int>().Distinct().ToList();
        var userNames = await dbContext.Users
            .AsNoTracking()
            .Where(u => ownerIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        var risks = await dbContext.Risks
            .AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .Select(x => new SummaryRiskRow(x.Probability, x.Impact, x.Status))
            .ToListAsync(cancellationToken);

        var requirementStatuses = await dbContext.Requirements
            .AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .Select(x => x.Status)
            .ToListAsync(cancellationToken);

        var documentTypeCount = await dbContext.DocumentTypes.CountAsync(cancellationToken);
        var documents = await dbContext.ProjectDocuments
            .AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .Select(x => new SummaryDocumentRow(x.Status))
            .ToListAsync(cancellationToken);

        return new ProjectSummaryDto(
            ProjectSummaryRules.OpenIssues(issues, today),
            ProjectSummaryRules.Risks(risks),
            ProjectSummaryRules.Requirements(requirementStatuses),
            ProjectSummaryRules.Documents(documentTypeCount, documents),
            ProjectSummaryRules.Workload(issues, userNames, today));
    }
}
