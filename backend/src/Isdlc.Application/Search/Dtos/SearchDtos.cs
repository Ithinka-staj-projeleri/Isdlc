using Isdlc.Domain.Projects;

namespace Isdlc.Application.Search.Dtos;

/// <summary>Up to 5 rows of one kind and how many match in all (docs/search-api.md → S4).</summary>
public record SearchGroupDto<T>(IReadOnlyList<T> Items, int TotalCount);

/// <summary>A project, a request or an add-on ("14 / EG-01").</summary>
public record SearchProjectDto(int Id, string Code, string Name, string CustomerName, ProjectStatus Status);

/// <summary>A process improvement proposal; Id is its project's id, which the detail page opens with.</summary>
public record SearchImprovementDto(int Id, string Code, string Name, ProjectStatus Status);

/// <summary>An open issue, a risk or a requirement. Status is the record's own status as text ("Open", "Approved").</summary>
public record SearchRecordDto(
    int Id, string Code, string Title, string Status, int ProjectId, string ProjectCode, string ProjectName);

/// <summary>Minutes of a meeting. A meeting has no title, so Title is the start of its agenda.</summary>
public record SearchMeetingDto(
    int Id, string Code, string Title, DateOnly Date, int ProjectId, string ProjectCode, string ProjectName);

/// <summary>GET /search: every group is sent, an empty one too.</summary>
public record SearchResultDto(
    SearchGroupDto<SearchProjectDto> Projects,
    SearchGroupDto<SearchImprovementDto> Improvements,
    SearchGroupDto<SearchRecordDto> OpenIssues,
    SearchGroupDto<SearchMeetingDto> Meetings,
    SearchGroupDto<SearchRecordDto> Risks,
    SearchGroupDto<SearchRecordDto> Requirements);
