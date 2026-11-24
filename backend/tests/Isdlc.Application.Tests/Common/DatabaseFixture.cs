using Isdlc.Application;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Domain.Identity;
using Isdlc.Domain.Tenants;
using Isdlc.Infrastructure;
using Isdlc.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;

namespace Isdlc.Application.Tests.Common;

/// <summary>
/// A real SQL Server in Docker, shared by every test in the "Database" collection.
/// Started once per test run: applies the real migrations and loads the real seed
/// (process flow, users, sample projects), the same way the API does on startup.
/// Two companies: Ithinka (the seed) and Deneme A.Ş. (the templates and its own users, TestTenants), plus an inactive
/// company the startup seed must skip. Requires Docker to be running.
/// </summary>
public class DatabaseFixture : IAsyncLifetime
{
    // Same image as docker-compose.yml.
    private readonly MsSqlContainer _container =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    private ServiceProvider _services = null!;

    /// <summary>Deneme A.Ş.: active, seeded with the templates at startup, users in TestTenants.</summary>
    public int DenemeId { get; private set; }

    /// <summary>An inactive company opened before the startup seed ran; it must have no templates.</summary>
    public int InactiveTenantId { get; private set; }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _container.GetConnectionString(),
            })
            .Build();

        var currentUser = new FixtureCurrentUser();
        _services = new ServiceCollection()
            .AddLogging()
            .AddApplication()
            .AddInfrastructure(configuration)
            .AddSingleton<ICurrentUserService>(currentUser)
            .BuildServiceProvider();

        await _services.InitializeDatabaseAsync(isDevelopment: true);

        // The other companies exist before the second startup, so the startup loop itself gives Deneme its templates
        // and skips the inactive one; it also shows a second start writes nothing new for Ithinka.
        DenemeId = await AddTenantAsync(TestTenants.DenemeCode, TestTenants.DenemeName, isActive: true);
        InactiveTenantId = await AddTenantAsync("pasif-sirket", "Pasif Şirket", isActive: false);
        await _services.InitializeDatabaseAsync(isDevelopment: true);
        await AddDenemeUsersAsync();

        // Same default as before: scope-resolved services run as the first Business Analyst.
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IsdlcDbContext>();
        currentUser.UserId = await db.Users
            .Where(u => u.Email == "elif.yilmaz@isdlc.local")
            .Select(u => (int?)u.Id)
            .SingleAsync();
    }

    /// <summary>
    /// A fresh DI scope, like one HTTP request of an Ithinka user. Dispose it at the end of the test.
    /// </summary>
    public AsyncServiceScope CreateScope() => _services.CreateAsyncScope();

    /// <summary>
    /// A fresh DI scope working as the given company: its DbContext sees only that company's records and writes new
    /// ones to it. The company comes from the scope, not from the TestUser given to a service, because the query filter
    /// reads the scope's ITenantContext (docs/multi-tenancy.md → Testler).
    /// </summary>
    public AsyncServiceScope CreateScope(int tenantId)
    {
        var scope = _services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(tenantId);
        return scope;
    }

    private async Task<int> AddTenantAsync(string code, string name, bool isActive)
    {
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IsdlcDbContext>();
        var tenant = new Tenant { Code = code, Name = name, IsActive = isActive, CreatedAt = DateTime.UtcNow };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        return tenant.Id;
    }

    private async Task AddDenemeUsersAsync()
    {
        await using var scope = CreateScope(DenemeId);
        var db = scope.ServiceProvider.GetRequiredService<IsdlcDbContext>();
        db.Users.AddRange(TestTenants.DenemeUsers.Select(u => new User
        {
            Email = u.Email,
            FullName = u.FullName,
            Role = u.Role,
            CreatedAt = DateTime.UtcNow,
        }));
        await db.SaveChangesAsync();
    }

    private sealed class FixtureCurrentUser : ICurrentUserService
    {
        public int? UserId { get; set; }

        public int? TenantId => Tenant.IthinkaId;
    }

    public async Task DisposeAsync()
    {
        await _services.DisposeAsync();
        await _container.DisposeAsync();
    }
}

/// <summary>
/// Put [Collection("Database")] on a test class to use the shared database.
/// </summary>
[CollectionDefinition("Database")]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>;