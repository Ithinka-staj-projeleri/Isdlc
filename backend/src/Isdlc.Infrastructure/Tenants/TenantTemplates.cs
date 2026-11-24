using Isdlc.Application.Common.Interfaces;
using Isdlc.Infrastructure.Persistence.Seeding;
using Isdlc.Infrastructure.QuestionBank;

namespace Isdlc.Infrastructure.Tenants;

/// <summary>
/// The template seed of the current company: flows and document types (DatabaseSeeder), then question sets
/// (QuestionSetSeeder). The same code runs at startup and when a new company is opened, so both get the same copy.
/// </summary>
public sealed class TenantTemplates(ITenantContext tenant, DatabaseSeeder seeder, QuestionSetSeeder questionSets)
    : ITenantTemplates
{
    public async Task ApplyAsync(CancellationToken cancellationToken = default)
    {
        if (tenant.TenantId is null)
        {
            throw new InvalidOperationException("Templates are copied to a company; set ITenantContext first.");
        }

        await seeder.SeedTemplatesAsync(cancellationToken);
        await questionSets.SeedAsync(cancellationToken);
    }
}
