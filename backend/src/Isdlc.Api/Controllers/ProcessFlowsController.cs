using Isdlc.Api.Common;
using Isdlc.Application.Process;
using Isdlc.Application.Process.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Isdlc.Api.Controllers;

/// <summary>
/// /api/v1/process-flows — the company's work flows with their stages and steps. Reading is for everyone; editing
/// (Faz 7 · A7) is for the company administrator and returns the flow with its inactive parts.
/// Contract: docs/configurability.md → Akış düzenleme.
/// </summary>
public class ProcessFlowsController(IProcessService processService, IProcessFlowEditService editService) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ProcessFlowListItemDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<ProcessFlowListItemDto>> List(CancellationToken cancellationToken)
    {
        return await processService.GetFlowsAsync(cancellationToken);
    }

    /// <summary>?includeInactive=true also lists inactive stages and steps, for the company administrator only.</summary>
    [HttpGet("{code}")]
    [ProducesResponseType<ProcessFlowDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ProcessFlowDto> Get(string code, [FromQuery] bool includeInactive, CancellationToken cancellationToken)
    {
        return await processService.GetFlowAsync(code, includeInactive, cancellationToken);
    }

    // ---- Editing (company administrator) ----

    /// <summary>The flow's name and which records it starts (usedFor).</summary>
    [HttpPut("{code}")]
    [ProducesResponseType<ProcessFlowDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ProcessFlowDto> Update(string code, UpdateFlowDto request, CancellationToken cancellationToken)
    {
        return await editService.UpdateFlowAsync(code, request, cancellationToken);
    }

    /// <summary>A new flow of the company made from this one; it starts no records until chosen.</summary>
    [HttpPost("{code}/copy")]
    [ProducesResponseType<ProcessFlowDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProcessFlowDto>> Copy(string code, CopyFlowDto request, CancellationToken cancellationToken)
    {
        var flow = await editService.CopyFlowAsync(code, request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { code = flow.Code }, flow);
    }

    /// <summary>Every stage and step of the flow in its new order.</summary>
    [HttpPut("{code}/order")]
    [ProducesResponseType<ProcessFlowDto>(StatusCodes.Status200OK)]
    public async Task<ProcessFlowDto> Reorder(string code, ReorderFlowDto request, CancellationToken cancellationToken)
    {
        return await editService.ReorderAsync(code, request, cancellationToken);
    }

    [HttpPost("{code}/stages")]
    [ProducesResponseType<ProcessFlowDto>(StatusCodes.Status200OK)]
    public async Task<ProcessFlowDto> AddStage(string code, SaveStageDto request, CancellationToken cancellationToken)
    {
        return await editService.AddStageAsync(code, request, cancellationToken);
    }

    [HttpPut("/api/v1/process-stages/{id:int}")]
    [ProducesResponseType<ProcessFlowDto>(StatusCodes.Status200OK)]
    public async Task<ProcessFlowDto> UpdateStage(int id, SaveStageDto request, CancellationToken cancellationToken)
    {
        return await editService.UpdateStageAsync(id, request, cancellationToken);
    }

    [HttpPost("/api/v1/process-stages/{id:int}/deactivate")]
    [ProducesResponseType<ProcessFlowDto>(StatusCodes.Status200OK)]
    public async Task<ProcessFlowDto> DeactivateStage(int id, FlowVersionDto request, CancellationToken cancellationToken)
    {
        return await editService.SetStageActiveAsync(id, active: false, request, cancellationToken);
    }

    [HttpPost("/api/v1/process-stages/{id:int}/activate")]
    [ProducesResponseType<ProcessFlowDto>(StatusCodes.Status200OK)]
    public async Task<ProcessFlowDto> ActivateStage(int id, FlowVersionDto request, CancellationToken cancellationToken)
    {
        return await editService.SetStageActiveAsync(id, active: true, request, cancellationToken);
    }

    /// <summary>A new step in a stage, after afterStepId or first in the stage. Steps are renumbered.</summary>
    [HttpPost("{code}/steps")]
    [ProducesResponseType<ProcessFlowDto>(StatusCodes.Status200OK)]
    public async Task<ProcessFlowDto> AddStep(string code, AddStepDto request, CancellationToken cancellationToken)
    {
        return await editService.AddStepAsync(code, request, cancellationToken);
    }

    [HttpPut("/api/v1/process-steps/{id:int}")]
    [ProducesResponseType<ProcessFlowDto>(StatusCodes.Status200OK)]
    public async Task<ProcessFlowDto> UpdateStep(int id, SaveStepDto request, CancellationToken cancellationToken)
    {
        return await editService.UpdateStepAsync(id, request, cancellationToken);
    }

    /// <summary>409 while a record waits on the step or another step returns to it.</summary>
    [HttpPost("/api/v1/process-steps/{id:int}/deactivate")]
    [ProducesResponseType<ProcessFlowDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ProcessFlowDto> DeactivateStep(int id, FlowVersionDto request, CancellationToken cancellationToken)
    {
        return await editService.SetStepActiveAsync(id, active: false, request, cancellationToken);
    }

    [HttpPost("/api/v1/process-steps/{id:int}/activate")]
    [ProducesResponseType<ProcessFlowDto>(StatusCodes.Status200OK)]
    public async Task<ProcessFlowDto> ActivateStep(int id, FlowVersionDto request, CancellationToken cancellationToken)
    {
        return await editService.SetStepActiveAsync(id, active: true, request, cancellationToken);
    }
}
