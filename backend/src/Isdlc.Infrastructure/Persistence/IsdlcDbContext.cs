using Isdlc.Application.Common.Interfaces;
using Isdlc.Domain.Audit;
using Isdlc.Domain.Identity;
using Isdlc.Domain.Meetings;
using Isdlc.Domain.OpenIssues;
using Isdlc.Domain.Process;
using Isdlc.Domain.Projects;
using Isdlc.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;

namespace Isdlc.Infrastructure.Persistence;

public class IsdlcDbContext(DbContextOptions<IsdlcDbContext> options) : DbContext(options), IApplicationDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProcessStage> ProcessStages => Set<ProcessStage>();
    public DbSet<ProcessStep> ProcessSteps => Set<ProcessStep>();
    public DbSet<ProcessStepJump> ProcessStepJumps => Set<ProcessStepJump>();
    public DbSet<StepRecord> StepRecords => Set<StepRecord>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<OpenIssue> OpenIssues => Set<OpenIssue>();
    public DbSet<Meeting> Meetings => Set<Meeting>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // All dates are stored and returned as UTC (see AGENTS.md, API rules).
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();

        // Enums are stored as text ("Active"), the same way the API sends them.
        var enumTypes = typeof(Project).Assembly.GetTypes().Where(t => t.IsEnum);
        foreach (var enumType in enumTypes)
        {
            configurationBuilder.Properties(enumType).HaveConversion<string>().HaveMaxLength(50);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Picks up every IEntityTypeConfiguration<T> in this project,
        // so each module only adds its own configuration class.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IsdlcDbContext).Assembly);

        // Records are never deleted (users are deactivated, history is kept),
        // so no relationship deletes its children.
        foreach (var foreignKey in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
        {
            foreignKey.DeleteBehavior = DeleteBehavior.Restrict;
        }
    }
}
