using Isdlc.Application.Identity;
using Isdlc.Application.Tests.Common;
using Isdlc.Infrastructure.Persistence;
using Isdlc.Infrastructure.Persistence.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Isdlc.Application.Tests.Identity;

/// <summary>
/// The development seeder that gives seed users the test password (Identity:TestPassword, from user-secrets).
/// It changes seed users, so every test runs in a transaction that is rolled back: the shared database stays as it was.
/// </summary>
[Collection("Database")]
public class UserPasswordSeederTests(DatabaseFixture database)
{
    private const string TestPassword = "Ekip-Test-Parolasi-1";

    private static UserPasswordSeeder Seeder(AsyncServiceScope scope, string? password)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Identity:TestPassword"] = password })
            .Build();
        return new UserPasswordSeeder(
            scope.ServiceProvider.GetRequiredService<IsdlcDbContext>(),
            scope.ServiceProvider.GetRequiredService<IPasswordHasher>(),
            configuration,
            NullLogger<UserPasswordSeeder>.Instance);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Seed_PasswordNotSet_ChangesNoUser(string? password)
    {
        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IsdlcDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var before = await db.Users.AsNoTracking().Select(u => new { u.Id, u.PasswordHash }).ToListAsync();

        await Seeder(scope, password).SeedAsync();

        var after = await db.Users.AsNoTracking().Select(u => new { u.Id, u.PasswordHash }).ToListAsync();
        Assert.Equal(before, after);
        await transaction.RollbackAsync();
    }

    [Fact]
    public async Task Seed_PasswordSet_GivesItOnlyToUsersWithoutAPassword()
    {
        // A user who already has a password keeps it.
        var withPassword = await TestUsers.CreateAsync(database, "Kendi-Parolasi-1");

        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IsdlcDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        await using var transaction = await db.Database.BeginTransactionAsync();

        await Seeder(scope, TestPassword).SeedAsync();

        var users = await db.Users.AsNoTracking().ToListAsync();
        Assert.DoesNotContain(users, u => u.PasswordHash == "");

        var analyst = users.Single(u => u.Email == TestProjects.Analyst);
        Assert.True(hasher.Verify(analyst.PasswordHash, TestPassword));

        var kept = users.Single(u => u.Id == withPassword.Id);
        Assert.Equal(withPassword.PasswordHash, kept.PasswordHash);
        Assert.False(hasher.Verify(kept.PasswordHash, TestPassword));

        await transaction.RollbackAsync();
    }

    [Fact]
    public async Task Seed_RunTwice_DoesNotChangeTheFirstHashes()
    {
        // The API runs the seeder on every start; the second run must not hash the passwords again.
        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IsdlcDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();

        await Seeder(scope, TestPassword).SeedAsync();
        var first = await db.Users.AsNoTracking().Select(u => new { u.Id, u.PasswordHash }).ToListAsync();
        await Seeder(scope, TestPassword).SeedAsync();
        var second = await db.Users.AsNoTracking().Select(u => new { u.Id, u.PasswordHash }).ToListAsync();

        Assert.Equal(first, second);
        await transaction.RollbackAsync();
    }
}
