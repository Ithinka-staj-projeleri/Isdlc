using Isdlc.Api.Common;
using Isdlc.Application.Common.Models;
using Isdlc.Application.Projects;
using Isdlc.Application.Projects.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Isdlc.Api.Controllers;

/// <summary>Requests and projects: /api/v1/projects. Contract: docs/projects-api.md</summary>
public class ProjectsController(IProjectService projectService) : ApiControllerBase
{
    /// <summary>
    /// Filters: ?status=Active&amp;phase=Request&amp;search=portal&amp;businessAnalystId=1. Paging: ?page=1&amp;pageSize=20.
    /// phase=Request is the "Talepler" menu (stages 1–3), phase=Project the "Projeler" menu (stages 4–13).
    /// </summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<ProjectListItemDto>>(StatusCodes.Status200OK)]
    public async Task<PagedResult<ProjectListItemDto>> List(
        [FromQuery] ProjectFilter filter, [FromQuery] PageRequest pageRequest, CancellationToken cancellationToken)
    {
        return await projectService.ListAsync(filter, pageRequest, cancellationToken);
    }

    [HttpGet("{projectId:int}")]
    [ProducesResponseType<ProjectDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ProjectDto> Get(int projectId, CancellationToken cancellationToken)
    {
        return await projectService.GetAsync(projectId, cancellationToken);
    }

    /// <summary>
    /// A new request. It starts Active at the first step of its flow and gets the next Proje No (e.g. 19).
    /// Only a Business Analyst can open a request.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<ProjectDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProjectDto>> Create(CreateProjectDto request, CancellationToken cancellationToken)
    {
        var created = await projectService.CreateAsync(request, cancellationToken);
        return Created($"/api/v1/projects/{created.Id}", created);
    }

    /// <summary>Updates the basic information of an active project. Status and process fields are not changed here.</summary>
    [HttpPut("{projectId:int}")]
    [ProducesResponseType<ProjectDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ProjectDto> Update(int projectId, UpdateProjectDto request, CancellationToken cancellationToken)
    {
        return await projectService.UpdateAsync(projectId, request, cancellationToken);
    }
}
