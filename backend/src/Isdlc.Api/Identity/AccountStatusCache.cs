using Isdlc.Application.Identity;
using Microsoft.Extensions.Caching.Memory;

namespace Isdlc.Api.Identity;

/// <summary>Forgets the answer AccountGateMiddleware keeps for a minute, so a deactivated user is signed out at once.</summary>
public class AccountStatusCache(IMemoryCache cache) : IAccountStatusCache
{
    public void Forget(int userId, int? tenantId) => cache.Remove(AccountGateMiddleware.CacheKey(userId, tenantId));
}
