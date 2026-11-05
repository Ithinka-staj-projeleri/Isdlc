using Isdlc.Api.Common;
using Isdlc.Application.Process;
using Isdlc.Application.Process.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Isdlc.Api.Controllers;

/// <summary>/api/v1/process-stages — the ISDLC flow (13 stages and their steps).</summary>
public class ProcessStagesController(IProcessService processService) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ProcessStageDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<ProcessStageDto>> List(CancellationToken cancellationToken)
    {
        return await processService.GetStagesAsync(cancellationToken);
    }
}
