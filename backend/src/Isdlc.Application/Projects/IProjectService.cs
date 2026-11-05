using Isdlc.Application.Projects.Dtos;

namespace Isdlc.Application.Projects;

/// <summary>
/// Requests and projects. Contract: docs/projects-api.md
/// </summary>
public interface IProjectService
{
    /// <summary>404 when the project does not exist.</summary>
    Task<ProjectDto> GetAsync(int projectId, CancellationToken cancellationToken);

    /// <summary>
    /// A new request: Active, at the first step of the flow, with the next code of the year.
    /// Only an active Business Analyst can open one (403 otherwise).
    /// </summary>
    Task<ProjectDto> CreateAsync(CreateProjectDto request, CancellationToken cancellationToken);
}
