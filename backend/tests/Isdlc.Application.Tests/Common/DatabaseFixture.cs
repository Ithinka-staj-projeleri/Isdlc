using Isdlc.Application;
using Isdlc.Application.Common.Interfaces;
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
/// Requires Docker to be running.
/// </summary>
public class DatabaseFixture : IAsyncLifetime
{
    // Same image as docker-compose.yml.
    private readonly MsSqlContainer _container =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    private ServiceProvider _services = null!;

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

        // Same default as before: scope-resolved services run as the first Business Analyst.
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IsdlcDbContext>();
        currentUser.UserId = await db.Users
            .Where(u => u.Email == "elif.yilmaz@isdlc.local")
            .Select(u => (int?)u.Id)
            .SingleAsync();
    }

    /// <summary>
    /// A fresh DI scope, like one HTTP request. Dispose it at the end of the test.
    /// </summary>
    public AsyncServiceScope CreateScope() => _services.CreateAsyncScope();

    private sealed class FixtureCurrentUser : ICurrentUserService
    {
        public int? UserId { get; set; }
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