using Isdlc.Api.Common;
using Isdlc.Application.Common.Models;
using Isdlc.Application.OpenIssues;
using Isdlc.Application.OpenIssues.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Isdlc.Api.Controllers;

/// <summary>
/// The project's Open Issues List: /api/v1/projects/{projectId}/open-issues.
/// It is a sub-resource of projects, so the routes are written in full ("/" ignores the base route).
/// </summary>
public class OpenIssuesController(IOpenIssueService openIssueService) : ApiControllerBase
{
    private const string Route = "/api/v1/projects/{projectId:int}/open-issues";

    /// <summary>Filters: ?status=Open&amp;type=Action&amp;overdueOnly=true. Paging: ?page=1&amp;pageSize=20</summary>
    [HttpGet(Route)]
    [ProducesResponseType<PagedResult<OpenIssueListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<PagedResult<OpenIssueListItemDto>> List(
        int projectId, [FromQuery] OpenIssueFilter filter, [FromQuery] PageRequest pageRequest,
        CancellationToken cancellationToken)
    {
        return await openIssueService.ListAsync(projectId, filter, pageRequest, cancellationToken);
    }

    [HttpGet(Route + "/{openIssueId:int}")]
    [ProducesResponseType<OpenIssueDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<OpenIssueDto> Get(int projectId, int openIssueId, CancellationToken cancellationToken)
    {
        return await openIssueService.GetAsync(projectId, openIssueId, cancellationToken);
    }

    [HttpPost(Route)]
    [ProducesResponseType<OpenIssueDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OpenIssueDto>> Create(
        int projectId, CreateOpenIssueDto request, CancellationToken cancellationToken)
    {
        var created = await openIssueService.CreateAsync(projectId, request, cancellationToken);
        return Created($"/api/v1/projects/{projectId}/open-issues/{created.Id}", created);
    }

    /// <summary>Updates the content (type, title, description, owner, due date). The status is changed separately.</summary>
    [HttpPut(Route + "/{openIssueId:int}")]
    [ProducesResponseType<OpenIssueDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<OpenIssueDto> Update(
        int projectId, int openIssueId, UpdateOpenIssueDto request, CancellationToken cancellationToken)
    {
        return await openIssueService.UpdateAsync(projectId, openIssueId, request, cancellationToken);
    }

    /// <summary>Opens, starts or closes the record (the "Kapat" / "Yeniden aç" buttons).</summary>
    [HttpPut(Route + "/{openIssueId:int}/status")]
    [ProducesResponseType<OpenIssueDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<OpenIssueDto> UpdateStatus(
        int projectId, int openIssueId, UpdateOpenIssueStatusDto request, CancellationToken cancellationToken)
    {
        return await openIssueService.UpdateStatusAsync(projectId, openIssueId, request, cancellationToken);
    }
}
