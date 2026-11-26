namespace Isdlc.Application.Tenants.Dtos;

/// <summary>
/// GET and PUT /tenants/current/settings: the company's two AI switches (docs/multi-tenancy.md → Şirket Yöneticisi), its
/// model and monthly token limit (Faz 7 · A9; empty: the service default / no limit).
/// </summary>
public record TenantSettingsDto(
    bool AiEnabled, bool AiDataSharingAllowed, string? AiModel = null, int? AiMonthlyTokenLimit = null);
