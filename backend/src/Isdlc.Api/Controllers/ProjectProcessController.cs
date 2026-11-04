using Isdlc.Api.Common;
using Isdlc.Application.Process;
using Isdlc.Application.Process.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Isdlc.Api.Controllers;

/// <summary>
/// A project's progress through the ISDLC flow: /api/v1/projects/{projectId}/process.
/// It is a sub-resource of projects, so the routes are written in full ("/" ignores the base route).
/// </summary>
public class ProjectProcessController(IProcessService processService) : ApiControllerBase
{
    [HttpGet("/api/v1/projects/{projectId:int}/process")]
    [ProducesResponseType<ProjectProcessDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ProjectProcessDto> Get(int projectId, CancellationToken cancellationToken)
    {
        return await processService.GetProjectProcessAsync(projectId, cancellationToken);
    }
}
