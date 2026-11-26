using Isdlc.Api.Common;
using Isdlc.Application.Common.Models;
using Isdlc.Application.Identity.Dtos;
using Isdlc.Application.Tenants;
using Isdlc.Application.Tenants.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Isdlc.Api.Controllers;

/// <summary>
/// /api/v1/tenants — companies (Faz 6). The list, opening and editing are the Platform Administrator's (403 for
/// anyone else, T4); "current" is every user's; "current/settings" (the AI switches, T6) is the company administrator's.
/// Contract: docs/multi-tenancy.md → Uç noktalar.
/// </summary>
public class TenantsController(ITenantService tenantService, ITenantSettingsService settingsService) : ApiControllerBase
{
    /// <summary>Every company, inactive ones too, by name. Paging: ?page=1&amp;pageSize=20</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<TenantDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<PagedResult<TenantDto>> List([FromQuery] PageRequest pageRequest, CancellationToken cancellationToken)
    {
        return await tenantService.ListAsync(pageRequest, cancellationToken);
    }

    /// <summary>Opens a company: the company, a copy of the templates and its first administrator, together.</summary>
    [HttpPost]
    [ProducesResponseType<TenantDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TenantDto>> Create(CreateTenantDto request, CancellationToken cancellationToken)
    {
        var tenant = await tenantService.CreateAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, tenant);
    }

    /// <summary>Name and active; an inactive company's users cannot sign in.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType<TenantDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<TenantDto> Update(int id, UpdateTenantDto request, CancellationToken cancellationToken)
    {
        return await tenantService.UpdateAsync(id, request, cancellationToken);
    }

    /// <summary>The signed-in user's company, for the top bar.</summary>
    [HttpGet("current")]
    [ProducesResponseType<CurrentTenantDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<CurrentTenantDto> Current(CancellationToken cancellationToken)
    {
        return await tenantService.GetCurrentAsync(cancellationToken);
    }

    /// <summary>The signed-in user's company AI switches. Company administrators only.</summary>
    [HttpGet("current/settings")]
    [ProducesResponseType<TenantSettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<TenantSettingsDto> GetSettings(CancellationToken cancellationToken)
    {
        return await settingsService.GetAsync(cancellationToken);
    }

    /// <summary>Turns the company's AI assistants and its permission to send text out on or off. Company administrators only.</summary>
    [HttpPut("current/settings")]
    [ProducesResponseType<TenantSettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<TenantSettingsDto> UpdateSettings(UpdateTenantSettingsDto dto, CancellationToken cancellationToken)
    {
        return await settingsService.UpdateAsync(dto, cancellationToken);
    }
}
