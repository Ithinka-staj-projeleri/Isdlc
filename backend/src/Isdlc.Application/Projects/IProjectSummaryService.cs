using Isdlc.Application.Projects.Dtos;

namespace Isdlc.Application.Projects;

/// <summary>The project overview (Faz 5 · F1). Contract: docs/projects-api.md → "Proje özeti".</summary>
public interface IProjectSummaryService
{
    /// <summary>Counted from the project's records as they are now. Any signed-in user; 404 when the project does not exist.</summary>
    Task<ProjectSummaryDto> GetAsync(int projectId, CancellationToken cancellationToken);
}
