using Isdlc.Api.Common;
using Isdlc.Application.Common.Models;
using Isdlc.Application.ProcessImprovements;
using Isdlc.Application.ProcessImprovements.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Isdlc.Api.Controllers;

/// <summary>
/// Process improvement proposals: /api/v1/process-improvements. Contract: docs/process-improvement-api.md
/// {id} is the proposal's project id; its steps, open issues, minutes and history use the /projects/{id}/... routes.
/// </summary>
public class ProcessImprovementsController(IProcessImprovementService service) : ApiControllerBase
{
    /// <summary>Filters: ?status=Active&amp;stageOrder=1. Paging: ?page=1&amp;pageSize=20</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<ImprovementListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<PagedResult<ImprovementListItemDto>> List(
        [FromQuery] ImprovementFilter filter, [FromQuery] PageRequest pageRequest, CancellationToken cancellationToken)
    {
        return await service.ListAsync(filter, pageRequest, cancellationToken);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<ImprovementDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ImprovementDto> Get(int id, CancellationToken cancellationToken)
    {
        return await service.GetAsync(id, cancellationToken);
    }

    [HttpPost]
    [ProducesResponseType<ImprovementDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ImprovementDto>> Create(CreateImprovementDto request, CancellationToken cancellationToken)
    {
        var created = await service.CreateAsync(request, cancellationToken);
        return Created($"/api/v1/process-improvements/{created.Id}", created);
    }

    /// <summary>Section 1: the proposal itself.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType<ImprovementDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ImprovementDto> Update(int id, UpdateImprovementDto request, CancellationToken cancellationToken)
    {
        return await service.UpdateAsync(id, request, cancellationToken);
    }

    /// <summary>Section 2: impact, frequency and ease (1–5), all three or none.</summary>
    [HttpPut("{id:int}/prioritization")]
    [ProducesResponseType<ImprovementDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ImprovementDto> SavePrioritization(
        int id, SavePrioritizationDto request, CancellationToken cancellationToken)
    {
        return await service.SavePrioritizationAsync(id, request, cancellationToken);
    }

    /// <summary>Section 3: the study (Çalışma Dokümanı).</summary>
    [HttpPut("{id:int}/study")]
    [ProducesResponseType<ImprovementDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ImprovementDto> SaveStudy(int id, SaveStudyDto request, CancellationToken cancellationToken)
    {
        return await service.SaveStudyAsync(id, request, cancellationToken);
    }

    /// <summary>Section 4: the pilot evaluation (Pilot Değerlendirme Raporu).</summary>
    [HttpPut("{id:int}/pilot-evaluation")]
    [ProducesResponseType<ImprovementDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ImprovementDto> SavePilotEvaluation(
        int id, SavePilotEvaluationDto request, CancellationToken cancellationToken)
    {
        return await service.SavePilotEvaluationAsync(id, request, cancellationToken);
    }
}
