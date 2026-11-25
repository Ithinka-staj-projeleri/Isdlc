using Isdlc.Api.Common;
using Isdlc.Application.Tenants;
using Isdlc.Application.Tenants.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Isdlc.Api.Controllers;

/// <summary>/api/v1/tenants — the companies (Faz 6). Today: the company administrator's AI switches (docs/multi-tenancy.md).</summary>
public class TenantsController(ITenantSettingsService settingsService) : ApiControllerBase
{
    /// <summary>The signed-in user's company AI switches. Company administrators only.</summary>
    [HttpGet("current/settings")]
    [ProducesResponseType<TenantSettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<TenantSettingsDto> GetSettings(CancellationToken cancellationToken)
    {
        return await settingsService.GetAsync(cancellationToken);
    }

    /// <summary>Turns the company's AI assistants and its permission to send text out on or off. Company administrators only.</summary>
    [HttpPut("current/settings")]
    [ProducesResponseType<TenantSettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<TenantSettingsDto> UpdateSettings(UpdateTenantSettingsDto dto, CancellationToken cancellationToken)
    {
        return await settingsService.UpdateAsync(dto, cancellationToken);
    }
}
