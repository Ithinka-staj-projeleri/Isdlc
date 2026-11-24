using Isdlc.Application.Common.Interfaces;
using Isdlc.Domain.Tenants;
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

        // Nobody is signed in at startup, so the seed says which company it fills. Ithinka is the only company until
        // T3 copies the templates to every company (docs/multi-tenancy.md → Seed ve şablon).
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(Tenant.IthinkaId);

        await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedAsync(includeSampleProjects: isDevelopment);

        // The question bank follows its seed files on every start (docs/question-bank-api.md → Seed).
        await scope.ServiceProvider.GetRequiredService<QuestionSetSeeder>().SeedAsync();
    }
}