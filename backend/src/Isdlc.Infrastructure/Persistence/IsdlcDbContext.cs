using Microsoft.EntityFrameworkCore;

namespace Isdlc.Infrastructure.Persistence;

public class IsdlcDbContext(DbContextOptions<IsdlcDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Picks up every IEntityTypeConfiguration<T> in this project,
        // so each module only adds its own configuration class.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IsdlcDbContext).Assembly);
    }
}
