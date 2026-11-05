using Isdlc.Application.Common.Models;
using Isdlc.Application.Projects.Dtos;

namespace Isdlc.Application.Projects;

/// <summary>
/// Requests and projects. Contract: docs/projects-api.md
/// </summary>
public interface IProjectService
{
    /// <summary>Filtered and paged; newest request first.</summary>
    Task<PagedResult<ProjectListItemDto>> ListAsync(
        ProjectFilter filter, PageRequest pageRequest, CancellationToken cancellationToken);

    /// <summary>404 when the project does not exist.</summary>
    Task<ProjectDto> GetAsync(int projectId, CancellationToken cancellationToken);

    /// <summary>
    /// A new request: Active, at the first step of the flow, with the next code of the year.
    /// Only an active Business Analyst can open one (403 otherwise).
    /// </summary>
    Task<ProjectDto> CreateAsync(CreateProjectDto request, CancellationToken cancellationToken);

    /// <summary>
    /// Updates the basic information. 404 when the project does not exist, 409 when it is not active.
    /// Status, current step, priority and level are owned by Process and Assessment and never change here.
    /// </summary>
    Task<ProjectDto> UpdateAsync(int projectId, UpdateProjectDto request, CancellationToken cancellationToken);
}
