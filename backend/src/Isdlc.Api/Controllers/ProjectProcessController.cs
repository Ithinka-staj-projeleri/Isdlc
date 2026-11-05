using Isdlc.Api.Common;
using Isdlc.Application.Common.Models;
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

    /// <summary>Process history, newest first. Paging: ?page=1&amp;pageSize=20</summary>
    [HttpGet("/api/v1/projects/{projectId:int}/step-records")]
    [ProducesResponseType<PagedResult<StepRecordDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<PagedResult<StepRecordDto>> ListStepRecords(
        int projectId, [FromQuery] PageRequest pageRequest, CancellationToken cancellationToken)
    {
        return await processService.GetStepRecordsAsync(projectId, pageRequest, cancellationToken);
    }

    /// <summary>Completes the current (non-decision) step. Returns the updated process state.</summary>
    [HttpPost("/api/v1/projects/{projectId:int}/process/complete")]
    [ProducesResponseType<ProjectProcessDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ProjectProcessDto> Complete(
        int projectId, CompleteStepDto request, CancellationToken cancellationToken)
    {
        return await processService.CompleteStepAsync(projectId, request, cancellationToken);
    }

    /// <summary>Answers the current decision step with Yes or No. Returns the updated process state.</summary>
    [HttpPost("/api/v1/projects/{projectId:int}/process/answer")]
    [ProducesResponseType<ProjectProcessDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ProjectProcessDto> Answer(
        int projectId, AnswerStepDto request, CancellationToken cancellationToken)
    {
        return await processService.AnswerStepAsync(projectId, request, cancellationToken);
    }
}
