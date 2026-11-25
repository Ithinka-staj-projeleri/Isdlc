using Isdlc.Application.Common.Interfaces;
using Isdlc.Domain.Tenants;
using Isdlc.Infrastructure.Persistence.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Isdlc.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    /// <summary>
    /// Runs once at startup: applies migrations (development only), then seeds every active company in turn
    /// (docs/multi-tenancy.md → Seed ve şablon, "Açılış sırası").
    /// </summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, bool isDevelopment)
    {
        List<int> tenantIds;
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IsdlcDbContext>();
            if (isDevelopment)
            {
                await db.Database.MigrateAsync();
            }

            // Tenants has no query filter; nobody is signed in at startup.
            tenantIds = await db.Tenants.Where(t => t.IsActive).OrderBy(t => t.Id).Select(t => t.Id).ToListAsync();
        }

        foreach (var tenantId in tenantIds)
        {
            await SeedCompanyAsync(services, tenantId, isDevelopment);
        }

        // Last, with no company set: the Platform Administrator belongs to none (Faz 6 · T4).
        await using (var scope = services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<PlatformAdminSeeder>().SeedAsync(isDevelopment);
        }
    }

    /// <summary>
    /// One company in its own scope and transaction: a fresh DbContext, so nothing tracked for one company is saved
    /// to another, and a company whose seed fails leaves nothing half written.
    /// </summary>
    private static async Task SeedCompanyAsync(IServiceProvider services, int tenantId, bool isDevelopment)
    {
        await using var scope = services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<IsdlcDbContext>();

        await using var transaction = await db.Database.BeginTransactionAsync();

        // Flows, document types and question sets: every company gets the templates it does not have yet.
        await scope.ServiceProvider.GetRequiredService<ITenantTemplates>().ApplyAsync();

        // Seed/users.json and the sample projects are Ithinka's development data, never another company's.
        if (tenantId == Tenant.IthinkaId)
        {
            await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>()
                .SeedDevelopmentCompanyAsync(includeSampleProjects: isDevelopment);
        }

        await transaction.CommitAsync();
    }
}
