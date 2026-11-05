using Isdlc.Api.Common;
using Isdlc.Application.Projects;
using Isdlc.Application.Projects.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Isdlc.Api.Controllers;

/// <summary>Requests and projects: /api/v1/projects. Contract: docs/projects-api.md</summary>
public class ProjectsController(IProjectService projectService) : ApiControllerBase
{
    [HttpGet("{projectId:int}")]
    [ProducesResponseType<ProjectDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ProjectDto> Get(int projectId, CancellationToken cancellationToken)
    {
        return await projectService.GetAsync(projectId, cancellationToken);
    }

    /// <summary>A new request. It starts Active at step 1.1 and gets the next code of the year (ISDLC-2026-019).</summary>
    [HttpPost]
    [ProducesResponseType<ProjectDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProjectDto>> Create(CreateProjectDto request, CancellationToken cancellationToken)
    {
        var created = await projectService.CreateAsync(request, cancellationToken);
        return Created($"/api/v1/projects/{created.Id}", created);
    }
}
