using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Identity.Dtos;
using Isdlc.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Isdlc.Application.Identity;

public class UserService(IApplicationDbContext dbContext) : IUserService
{
    public async Task<IReadOnlyList<UserLookupDto>> ListActiveAsync(Permission? permission, CancellationToken cancellationToken)
    {
        var query = dbContext.Users.AsNoTracking().Where(u => u.IsActive);

        if (permission is { } wanted)
        {
            query = query.Where(u => u.Permissions.Any(p => p.Permission == wanted));
        }

        return await query
            .OrderBy(u => u.FullName)
            .Select(u => new UserLookupDto(
                u.Id, u.FullName, u.Permissions.OrderBy(p => p.Permission).Select(p => p.Permission).ToList()))
            .ToListAsync(cancellationToken);
    }
}
