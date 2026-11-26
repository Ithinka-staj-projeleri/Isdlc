namespace Isdlc.Application.Common.Interfaces;

/// <summary>
/// The user making the current request.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>Null when nobody is signed in.</summary>
    int? UserId { get; }

    /// <summary>The user's company, from the JWT "tenant" claim. Null when nobody is signed in and for the Platform Administrator.</summary>
    int? TenantId { get; }

    /// <summary>Şirket Yöneticisi: manages the users of their company (JWT "tenantAdmin").</summary>
    bool IsTenantAdmin { get; }

    /// <summary>Platform Yöneticisi: opens and deactivates companies (JWT "platformAdmin").</summary>
    bool IsPlatformAdmin { get; }
}
