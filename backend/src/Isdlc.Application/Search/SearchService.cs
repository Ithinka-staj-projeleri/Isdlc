using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Search.Dtos;
using Isdlc.Domain.OpenIssues;
using Isdlc.Domain.Process;
using Isdlc.Domain.Projects;
using Isdlc.Domain.Requirements;
using Isdlc.Domain.Risks;
using Microsoft.EntityFrameworkCore;

namespace Isdlc.Application.Search;

/// <summary>
/// Searches codes and titles only, never long texts (S1). The database collation ignores case.
/// Each group: open ones first, then the newest (S5). Contract: docs/search-api.md.
/// </summary>
public class SearchService(IApplicationDbContext dbContext) : ISearchService
{
    public const int MinLength = 2;
    public const int MaxLength = 100;

    /// <summary>Rows per group (S4).</summary>
    public const int GroupSize = 5;

    /// <summary>A meeting's title is the start of its agenda.</summary>
    public const int MeetingTitleLength = 200;

    public async Task<SearchResultDto> SearchAsync(string? text, CancellationToken cancellationToken)
    {
        var search = text?.Trim() ?? string.Empty;
        if (search.Length < MinLength)
        {
            throw new BusinessRuleException($"En az {MinLength} karakter girin.");
        }

        if (search.Length > MaxLength)
        {
            throw new BusinessRuleException($"En fazla {MaxLength} karakter olabilir.");
        }

        return new SearchResultDto(
            await ProjectsAsync(search, cancellationToken),
            await ImprovementsAsync(search, cancellationToken),
            await OpenIssuesAsync(search, cancellationToken),
            await MeetingsAsync(search, cancellationToken),
            await RisksAsync(search, cancellationToken),
            await RequirementsAsync(search, cancellationToken));
    }

    private async Task<SearchGroupDto<SearchProjectDto>> ProjectsAsync(string search, CancellationToken cancellationToken)
    {
        // Proje No as in the project list: exact ("14" does not find 114), and it also finds the project's add-ons.
        var query = dbContext.Projects
            .AsNoTracking()
            .Where(p => p.ProcessFlow.Kind == ProcessFlowKind.Request)
            .Where(p => p.Code == search
                || (p.ParentProject != null && p.ParentProject.Code == search)
                || p.Name.Contains(search)
                || p.CustomerName.Contains(search));

        var items = await query
            .OrderBy(p => p.Status == ProjectStatus.Active || p.Status == ProjectStatus.Postponed ? 0 : 1)
            .ThenByDescending(p => p.Id)
            .Take(GroupSize)
            .Select(p => new SearchProjectDto(p.Id, p.Code, p.Name, p.CustomerName, p.Status))
            .ToListAsync(cancellationToken);

        return new SearchGroupDto<SearchProjectDto>(items, await query.CountAsync(cancellationToken));
    }

    private async Task<SearchGroupDto<SearchImprovementDto>> ImprovementsAsync(
        string search, CancellationToken cancellationToken)
    {
        var query = dbContext.Projects
            .AsNoTracking()
            .Where(p => p.ProcessFlow.Kind == ProcessFlowKind.Improvement)
            .Where(p => p.Code.Contains(search) || p.Name.Contains(search));

        var items = await query
            .OrderBy(p => p.Status == ProjectStatus.Active || p.Status == ProjectStatus.Postponed ? 0 : 1)
            .ThenByDescending(p => p.Id)
            .Take(GroupSize)
            .Select(p => new SearchImprovementDto(p.Id, p.Code, p.Name, p.Status))
            .ToListAsync(cancellationToken);

        return new SearchGroupDto<SearchImprovementDto>(items, await query.CountAsync(cancellationToken));
    }

    private async Task<SearchGroupDto<SearchRecordDto>> OpenIssuesAsync(string search, CancellationToken cancellationToken)
    {
        var query = dbContext.OpenIssues
            .AsNoTracking()
            .Where(x => x.Code.Contains(search) || x.Title.Contains(search));

        var rows = await query
            .OrderBy(x => x.Status == OpenIssueStatus.Closed ? 1 : 0)
            .ThenByDescending(x => x.Id)
            .Take(GroupSize)
            .Select(x => new RecordRow<OpenIssueStatus>(
                x.Id, x.Code, x.Title, x.Status, x.ProjectId, x.Project.Code, x.Project.Name))
            .ToListAsync(cancellationToken);

        return new SearchGroupDto<SearchRecordDto>(ToDtos(rows), await query.CountAsync(cancellationToken));
    }

    private async Task<SearchGroupDto<SearchMeetingDto>> MeetingsAsync(string search, CancellationToken cancellationToken)
    {
        var query = dbContext.Meetings
            .AsNoTracking()
            .Where(x => x.Code.Contains(search) || x.Agenda.Contains(search));

        // Minutes are never closed: the latest meeting first.
        var items = await query
            .OrderByDescending(x => x.Date)
            .ThenByDescending(x => x.Id)
            .Take(GroupSize)
            .Select(x => new SearchMeetingDto(
                x.Id,
                x.Code,
                x.Agenda.Length > MeetingTitleLength ? x.Agenda.Substring(0, MeetingTitleLength) : x.Agenda,
                x.Date,
                x.ProjectId,
                x.Project.Code,
                x.Project.Name))
            .ToListAsync(cancellationToken);

        return new SearchGroupDto<SearchMeetingDto>(items, await query.CountAsync(cancellationToken));
    }

    private async Task<SearchGroupDto<SearchRecordDto>> RisksAsync(string search, CancellationToken cancellationToken)
    {
        var query = dbContext.Risks
            .AsNoTracking()
            .Where(x => x.Code.Contains(search) || x.Title.Contains(search));

        var rows = await query
            .OrderBy(x => x.Status == RiskStatus.Closed ? 1 : 0)
            .ThenByDescending(x => x.Id)
            .Take(GroupSize)
            .Select(x => new RecordRow<RiskStatus>(
                x.Id, x.Code, x.Title, x.Status, x.ProjectId, x.Project.Code, x.Project.Name))
            .ToListAsync(cancellationToken);

        return new SearchGroupDto<SearchRecordDto>(ToDtos(rows), await query.CountAsync(cancellationToken));
    }

    private async Task<SearchGroupDto<SearchRecordDto>> RequirementsAsync(
        string search, CancellationToken cancellationToken)
    {
        var query = dbContext.Requirements
            .AsNoTracking()
            .Where(x => x.Code.Contains(search) || x.Title.Contains(search));

        // A cancelled requirement counts as closed.
        var rows = await query
            .OrderBy(x => x.Status == RequirementStatus.Cancelled ? 1 : 0)
            .ThenByDescending(x => x.Id)
            .Take(GroupSize)
            .Select(x => new RecordRow<RequirementStatus>(
                x.Id, x.Code, x.Title, x.Status, x.ProjectId, x.Project.Code, x.Project.Name))
            .ToListAsync(cancellationToken);

        return new SearchGroupDto<SearchRecordDto>(ToDtos(rows), await query.CountAsync(cancellationToken));
    }

    /// <summary>The status is turned into text after the query, so the database is not asked to format enums.</summary>
    private static List<SearchRecordDto> ToDtos<TStatus>(IEnumerable<RecordRow<TStatus>> rows)
        where TStatus : struct, Enum =>
        rows.Select(r => new SearchRecordDto(
                r.Id, r.Code, r.Title, r.Status.ToString(), r.ProjectId, r.ProjectCode, r.ProjectName))
            .ToList();

    private sealed record RecordRow<TStatus>(
        int Id, string Code, string Title, TStatus Status, int ProjectId, string ProjectCode, string ProjectName);
}
