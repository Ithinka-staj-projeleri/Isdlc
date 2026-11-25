namespace Isdlc.Application.Tenants.Dtos;

/// <summary>GET and PUT /tenants/current/settings: the company's two AI switches (docs/multi-tenancy.md → Şirket Yöneticisi).</summary>
public record TenantSettingsDto(bool AiEnabled, bool AiDataSharingAllowed);
