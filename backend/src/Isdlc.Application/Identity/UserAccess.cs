using Isdlc.Application.Common.Interfaces;
using Isdlc.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Isdlc.Application.Identity;

/// <summary>
/// A signed-in user's permissions, with the names of the company's administrators for the "whom to ask" part of a
/// refusal. Read from the database on each request, so a change by the administrator counts from the next one.
/// </summary>
public record UserAccess(int UserId, IReadOnlySet<Permission> Permissions, IReadOnlyList<string> TenantAdmins)
{
    public bool Has(Permission permission) => Permissions.Contains(permission);
}

/// <summary>
/// Permission queries every module shares. The company filter keeps them inside the request's company: another company's
/// user, permission or administrator is never found (docs/configurability.md → Giriş ve istek).
/// </summary>
public static class UserAccessQueries
{
    /// <summary>The active user's permissions and the company's administrators; null when the user is not an active one.</summary>
    public static async Task<UserAccess?> LoadAccessAsync(
        this IApplicationDbContext dbContext, int? userId, CancellationToken cancellationToken)
    {
        if (userId is not { } id
            || !await dbContext.Users.AnyAsync(u => u.Id == id && u.IsActive, cancellationToken))
        {
            return null;
        }

        var permissions = await dbContext.UserPermissions
            .AsNoTracking()
            .Where(p => p.UserId == id)
            .Select(p => p.Permission)
            .ToListAsync(cancellationToken);

        return new UserAccess(id, permissions.ToHashSet(), await dbContext.TenantAdminNamesAsync(cancellationToken));
    }

    /// <summary>The company's active administrators, by name.</summary>
    public static async Task<IReadOnlyList<string>> TenantAdminNamesAsync(
        this IApplicationDbContext dbContext, CancellationToken cancellationToken) =>
        await dbContext.Users
            .AsNoTracking()
            .Where(u => u.IsTenantAdmin && u.IsActive)
            .OrderBy(u => u.FullName)
            .Select(u => u.FullName)
            .ToListAsync(cancellationToken);

    /// <summary>The user is active and has the permission (e.g. a project's analyst must have Analysis).</summary>
    public static Task<bool> IsActiveUserWithAsync(
        this IApplicationDbContext dbContext, int userId, Permission permission, CancellationToken cancellationToken) =>
        dbContext.Users.AnyAsync(
            u => u.Id == userId && u.IsActive && u.Permissions.Any(p => p.Permission == permission), cancellationToken);
}
