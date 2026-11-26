using Isdlc.Application.Common.Models;
using Isdlc.Application.Identity.Dtos;
using Isdlc.Application.Tenants.Dtos;

namespace Isdlc.Application.Tenants;

/// <summary>
/// Companies (Faz 6 · T4). The list, opening and editing are the Platform Administrator's ("Bu işlem yalnızca platform
/// yöneticisine açıktır." for anyone else); the current company is every user's. Contract: docs/multi-tenancy.md → Uç noktalar.
/// </summary>
public interface ITenantService
{
    /// <summary>Every company, inactive ones too, by name.</summary>
    Task<PagedResult<TenantDto>> ListAsync(PageRequest pageRequest, CancellationToken cancellationToken);

    /// <summary>
    /// The company, a copy of the templates (flows, question sets, document types) and its first administrator, in one
    /// transaction: if one fails, nothing is saved. 400 bad code, 409 code or e-mail used.
    /// </summary>
    Task<TenantDto> CreateAsync(CreateTenantDto request, CancellationToken cancellationToken);

    /// <summary>Name and active. A deactivated company's users cannot sign in, and are signed out within a minute.</summary>
    Task<TenantDto> UpdateAsync(int tenantId, UpdateTenantDto request, CancellationToken cancellationToken);

    /// <summary>The signed-in user's company, for the top bar. 404 for the Platform Administrator, who has none.</summary>
    Task<CurrentTenantDto> GetCurrentAsync(CancellationToken cancellationToken);
}
