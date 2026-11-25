using Isdlc.Application.Tenants.Dtos;

namespace Isdlc.Application.Tenants;

/// <summary>
/// The signed-in user's company settings, for the Şirket Yöneticisi (Faz 6 · T6). Today only the AI switches; their effect
/// is in CompanyAiRules. Contract: docs/multi-tenancy.md → Şirket Yöneticisi.
/// </summary>
public interface ITenantSettingsService
{
    /// <summary>ForbiddenException unless the user is a company administrator.</summary>
    Task<TenantSettingsDto> GetAsync(CancellationToken cancellationToken);

    /// <summary>Saves both switches and returns them. ForbiddenException unless the user is a company administrator.</summary>
    Task<TenantSettingsDto> UpdateAsync(UpdateTenantSettingsDto dto, CancellationToken cancellationToken);
}
