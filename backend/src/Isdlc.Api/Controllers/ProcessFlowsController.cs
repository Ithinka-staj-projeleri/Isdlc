using Isdlc.Api.Common;
using Isdlc.Application.Process;
using Isdlc.Application.Process.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Isdlc.Api.Controllers;

/// <summary>/api/v1/process-flows — the company's work flows with their stages and steps.</summary>
public class ProcessFlowsController(IProcessService processService) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ProcessFlowListItemDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<ProcessFlowListItemDto>> List(CancellationToken cancellationToken)
    {
        return await processService.GetFlowsAsync(cancellationToken);
    }

    [HttpGet("{code}")]
    [ProducesResponseType<ProcessFlowDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ProcessFlowDto> Get(string code, CancellationToken cancellationToken)
    {
        return await processService.GetFlowAsync(code, cancellationToken);
    }
}
