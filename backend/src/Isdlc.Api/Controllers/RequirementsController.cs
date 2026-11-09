using Isdlc.Api.Common;
using Isdlc.Application.Common.Models;
using Isdlc.Application.Requirements;
using Isdlc.Application.Requirements.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Isdlc.Api.Controllers;

/// <summary>
/// The project's requirements: /api/v1/projects/{projectId}/requirements.
/// It is a sub-resource of projects, so the routes are written in full ("/" ignores the base route).
/// </summary>
public class RequirementsController(IRequirementService requirementService) : ApiControllerBase
{
    private const string Route = "/api/v1/projects/{projectId:int}/requirements";

    /// <summary>Filters: ?status=Approved&amp;type=Functional&amp;priority=MustHave&amp;search=mobil. Paging: ?page=1&amp;pageSize=20</summary>
    [HttpGet(Route)]
    [ProducesResponseType<PagedResult<RequirementListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<PagedResult<RequirementListItemDto>> List(
        int projectId, [FromQuery] RequirementFilter filter, [FromQuery] PageRequest pageRequest,
        CancellationToken cancellationToken)
    {
        return await requirementService.ListAsync(projectId, filter, pageRequest, cancellationToken);
    }

    [HttpGet(Route + "/{requirementId:int}")]
    [ProducesResponseType<RequirementDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<RequirementDto> Get(int projectId, int requirementId, CancellationToken cancellationToken)
    {
        return await requirementService.GetAsync(projectId, requirementId, cancellationToken);
    }

    [HttpPost(Route)]
    [ProducesResponseType<RequirementDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RequirementDto>> Create(
        int projectId, CreateRequirementDto request, CancellationToken cancellationToken)
    {
        var created = await requirementService.CreateAsync(projectId, request, cancellationToken);
        return Created($"/api/v1/projects/{projectId}/requirements/{created.Id}", created);
    }

    /// <summary>Updates the content (type, title, description, acceptance criteria, priority). The status is changed separately.</summary>
    [HttpPut(Route + "/{requirementId:int}")]
    [ProducesResponseType<RequirementDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<RequirementDto> Update(
        int projectId, int requirementId, UpdateRequirementDto request, CancellationToken cancellationToken)
    {
        return await requirementService.UpdateAsync(projectId, requirementId, request, cancellationToken);
    }

    /// <summary>Changes the status (the "Onayla" / "İptal et" buttons).</summary>
    [HttpPut(Route + "/{requirementId:int}/status")]
    [ProducesResponseType<RequirementDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<RequirementDto> UpdateStatus(
        int projectId, int requirementId, UpdateRequirementStatusDto request, CancellationToken cancellationToken)
    {
        return await requirementService.UpdateStatusAsync(projectId, requirementId, request, cancellationToken);
    }
}
