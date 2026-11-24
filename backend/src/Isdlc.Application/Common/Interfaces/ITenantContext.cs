namespace Isdlc.Application.Common.Interfaces;

/// <summary>
/// The company (tenant) of the current request: the global query filter shows only its records and TenantInterceptor
/// writes it on new ones. One per DI scope. In the API it comes from the signed-in user (ICurrentUserService.TenantId).
/// Rules: docs/multi-tenancy.md → Okuma kuralı.
/// </summary>
public interface ITenantContext
{
    /// <summary>Null when nobody is signed in (and for the Platform Administrator): then no tenant record is visible.</summary>
    int? TenantId { get; }

    /// <summary>
    /// Works as the given company for the rest of this scope. Only for code that runs without a signed-in user of that
    /// company: the startup seed and opening a new company. Services never call it.
    /// </summary>
    void Use(int? tenantId);
}
