using Isdlc.Application.Identity;
using Isdlc.Application.Tests.Common;
using Isdlc.Domain.Identity;
using Isdlc.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Isdlc.Application.Tests.Identity;

/// <summary>
/// Users made by Identity tests, so seed users are never changed. Every call gets a new e-mail address.
/// A Platform Administrator (isPlatformAdmin) is saved without a company or permissions whatever the scope works as.
/// permission: the user's one permission (null: none); permissions: several, in place of it.
/// The fixture sets no test password, so seed users have an empty password hash and cannot sign in.
/// </summary>
public static class TestUsers
{
    public static async Task<User> CreateAsync(
        DatabaseFixture database,
        string? password,
        bool isActive = true,
        Permission? permission = Permission.Development,
        string? fullName = null,
        int? tenantId = null,
        bool isTenantAdmin = false,
        bool mustChangePassword = false,
        bool isPlatformAdmin = false,
        IReadOnlyCollection<Permission>? permissions = null)
    {
        // A user of another company is saved in a scope working as that company (Ithinka by default).
        await using var scope = tenantId is { } id ? database.CreateScope(id) : database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IsdlcDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var unique = Guid.NewGuid().ToString("N")[..8];
        var user = new User
        {
            FullName = fullName ?? $"Test Kullanıcı {unique}",
            Email = $"test.{unique}@isdlc.local",
            PasswordHash = password is null ? string.Empty : hasher.Hash(password),
            IsPlatformAdmin = isPlatformAdmin,
            Permissions = isPlatformAdmin
                ? []
                : (permissions ?? (permission is { } one ? [one] : []))
                    .Select(p => new UserPermission { Permission = p }).ToList(),
            IsActive = isActive,
            IsTenantAdmin = isTenantAdmin,
            MustChangePassword = mustChangePassword,
            CreatedAt = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }
}
