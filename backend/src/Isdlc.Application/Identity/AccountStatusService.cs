using Isdlc.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Isdlc.Application.Identity;

public class AccountStatusService(IApplicationDbContext db) : IAccountStatusService
{
    public async Task<bool> IsActiveAsync(int userId, int? tenantId, CancellationToken cancellationToken)
    {
        // tenant: the signed-in user's own row, found by the id in their token. The Platform Administrator has no
        // company, so the filter would never find them (docs/multi-tenancy.md → Okuma kuralı).
        var user = await db.Users.IgnoreQueryFilters()
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.IsActive, u.TenantId })
            .SingleOrDefaultAsync(cancellationToken);

        if (user is not { IsActive: true } || user.TenantId != tenantId)
        {
            return false;
        }

        return user.TenantId is not { } id || await db.Tenants.AnyAsync(t => t.Id == id && t.IsActive, cancellationToken);
    }
}
