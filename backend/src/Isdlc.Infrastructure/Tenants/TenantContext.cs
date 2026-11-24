using Isdlc.Application.Common.Interfaces;

namespace Isdlc.Infrastructure.Tenants;

/// <summary>
/// The company of one DI scope. Follows the signed-in user until Use is called (seed, opening a company); after that it
/// keeps the given company for the rest of the scope.
/// </summary>
public sealed class TenantContext(ICurrentUserService currentUser) : ITenantContext
{
    private bool _overridden;
    private int? _tenantId;

    public int? TenantId => _overridden ? _tenantId : currentUser.TenantId;

    public void Use(int? tenantId)
    {
        _overridden = true;
        _tenantId = tenantId;
    }
}
