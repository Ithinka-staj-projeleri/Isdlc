using Isdlc.Api.Common;
using Isdlc.Application.Projects;
using Isdlc.Application.Projects.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Isdlc.Api.Controllers;

/// <summary>
/// /api/v1/custom-fields — the company's own request form fields (Faz 7 · A6). Everyone reads the active ones; adding and
/// changing is the company administrator's (403 for anyone else). Fields are never deleted: PUT with isActive false.
/// Contract: docs/configurability.md → Şirkete özel alanlar.
/// </summary>
public class CustomFieldsController(ICustomFieldService customFieldService) : ApiControllerBase
{
    /// <summary>The active fields in form order; ?includeInactive=true (company administrators) adds the inactive ones.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CustomFieldDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IReadOnlyList<CustomFieldDto>> List(
        [FromQuery] bool includeInactive, CancellationToken cancellationToken)
    {
        return await customFieldService.ListAsync(includeInactive, cancellationToken);
    }

    /// <summary>Adds an active field. The key and the type never change afterwards.</summary>
    [HttpPost]
    [ProducesResponseType<CustomFieldDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CustomFieldDto>> Create(CreateCustomFieldDto request, CancellationToken cancellationToken)
    {
        var field = await customFieldService.CreateAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, field);
    }

    /// <summary>Label, options, required, help text, order and active. 409 when a removed option is used in a skip rule.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType<CustomFieldDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<CustomFieldDto> Update(int id, UpdateCustomFieldDto request, CancellationToken cancellationToken)
    {
        return await customFieldService.UpdateAsync(id, request, cancellationToken);
    }
}
