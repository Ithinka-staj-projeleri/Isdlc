using Isdlc.Application.Identity;
using Isdlc.Application.Tests.Common;
using Isdlc.Domain.Identity;
using Isdlc.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Isdlc.Application.Tests.Identity;

/// <summary>
/// Users made by Identity tests, so seed users are never changed. Every call gets a new e-mail address.
/// The fixture sets no test password, so seed users have an empty password hash and cannot sign in.
/// </summary>
public static class TestUsers
{
    public static async Task<User> CreateAsync(
        DatabaseFixture database,
        string? password,
        bool isActive = true,
        UserRole role = UserRole.SoftwareDeveloper,
        string? fullName = null)
    {
        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IsdlcDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var unique = Guid.NewGuid().ToString("N")[..8];
        var user = new User
        {
            FullName = fullName ?? $"Test Kullanıcı {unique}",
            Email = $"test.{unique}@isdlc.local",
            PasswordHash = password is null ? string.Empty : hasher.Hash(password),
            Role = role,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }
}
