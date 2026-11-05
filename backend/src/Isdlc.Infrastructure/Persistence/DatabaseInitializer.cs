using Isdlc.Infrastructure.Persistence.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Isdlc.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    /// <summary>
    /// Runs once at startup: applies migrations (development only), then loads seed data.
    /// </summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, bool isDevelopment)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IsdlcDbContext>();

        if (isDevelopment)
        {
            await db.Database.MigrateAsync();
        }

        await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedAsync(includeSampleProjects: isDevelopment);
    }
}