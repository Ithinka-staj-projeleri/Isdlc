using Isdlc.Infrastructure.Persistence.Seeding;
using Isdlc.Infrastructure.QuestionBank;
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

        // The question bank follows its seed files on every start (docs/question-bank-api.md → Seed).
        await scope.ServiceProvider.GetRequiredService<QuestionSetSeeder>().SeedAsync();
    }
}