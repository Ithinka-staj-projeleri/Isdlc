using Isdlc.Api.Common;
using Isdlc.Application.Common.Models;
using Isdlc.Application.Risks;
using Isdlc.Application.Risks.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Isdlc.Api.Controllers;

/// <summary>
/// The project's risks: /api/v1/projects/{projectId}/risks. Contract: docs/risks-api.md
/// It is a sub-resource of projects, so the routes are written in full ("/" ignores the base route).
/// </summary>
public class RisksController(IRiskService riskService) : ApiControllerBase
{
    private const string Route = "/api/v1/projects/{projectId:int}/risks";

    /// <summary>
    /// Filters: ?status=Open&amp;category=Technical&amp;severity=High; a heat map cell: ?probability=4&amp;impact=4 (both together).
    /// Paging: ?page=1&amp;pageSize=20
    /// </summary>
    [HttpGet(Route)]
    [ProducesResponseType<PagedResult<RiskListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<PagedResult<RiskListItemDto>> List(
        int projectId, [FromQuery] RiskFilter filter, [FromQuery] PageRequest pageRequest,
        CancellationToken cancellationToken)
    {
        return await riskService.ListAsync(projectId, filter, pageRequest, cancellationToken);
    }

    /// <summary>Heat map: counts of the risks that are not closed per probability × impact cell.</summary>
    [HttpGet(Route + "/matrix")]
    [ProducesResponseType<RiskMatrixDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<RiskMatrixDto> Matrix(int projectId, CancellationToken cancellationToken)
    {
        return await riskService.GetMatrixAsync(projectId, cancellationToken);
    }

    [HttpGet(Route + "/{riskId:int}")]
    [ProducesResponseType<RiskDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<RiskDto> Get(int projectId, int riskId, CancellationToken cancellationToken)
    {
        return await riskService.GetAsync(projectId, riskId, cancellationToken);
    }

    [HttpPost(Route)]
    [ProducesResponseType<RiskDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RiskDto>> Create(int projectId, CreateRiskDto request, CancellationToken cancellationToken)
    {
        var created = await riskService.CreateAsync(projectId, request, cancellationToken);
        return Created($"/api/v1/projects/{projectId}/risks/{created.Id}", created);
    }

    /// <summary>Updates the risk, its status included (the status is a field of the form).</summary>
    [HttpPut(Route + "/{riskId:int}")]
    [ProducesResponseType<RiskDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<RiskDto> Update(
        int projectId, int riskId, UpdateRiskDto request, CancellationToken cancellationToken)
    {
        return await riskService.UpdateAsync(projectId, riskId, request, cancellationToken);
    }
}
