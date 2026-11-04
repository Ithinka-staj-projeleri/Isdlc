using Isdlc.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;

namespace Isdlc.Infrastructure.Persistence;

public class IsdlcDbContext(DbContextOptions<IsdlcDbContext> options) : DbContext(options)
{
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // All dates are stored and returned as UTC (see AGENTS.md, API rules).
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Picks up every IEntityTypeConfiguration<T> in this project,
        // so each module only adds its own configuration class.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IsdlcDbContext).Assembly);
    }
}
