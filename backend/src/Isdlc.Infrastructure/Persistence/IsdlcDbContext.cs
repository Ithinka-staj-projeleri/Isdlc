using System.Reflection;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Domain.Agents;
using Isdlc.Domain.Audit;
using Isdlc.Domain.Common;
using Isdlc.Domain.Documents;
using Isdlc.Domain.Identity;
using Isdlc.Domain.Meetings;
using Isdlc.Domain.Notifications;
using Isdlc.Domain.OpenIssues;
using Isdlc.Domain.Process;
using Isdlc.Domain.ProcessImprovements;
using Isdlc.Domain.Projects;
using Isdlc.Domain.QuestionBank;
using Isdlc.Domain.Requirements;
using Isdlc.Domain.Risks;
using Isdlc.Domain.Tenants;
using Isdlc.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;

namespace Isdlc.Infrastructure.Persistence;

public class IsdlcDbContext(DbContextOptions<IsdlcDbContext> options, ITenantContext tenant)
    : DbContext(options), IApplicationDbContext
{
    /// <summary>
    /// Read by the query filters on every query (EF Core evaluates a DbContext member per query, not once per model).
    /// Null: nobody of a company is signed in, so no tenant record is visible.
    /// </summary>
    private int? CurrentTenantId => tenant.TenantId;

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserPermission> UserPermissions => Set<UserPermission>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProcessFlow> ProcessFlows => Set<ProcessFlow>();
    public DbSet<ProcessStage> ProcessStages => Set<ProcessStage>();
    public DbSet<ProcessStep> ProcessSteps => Set<ProcessStep>();
    public DbSet<ProcessStepJump> ProcessStepJumps => Set<ProcessStepJump>();
    public DbSet<StepRecord> StepRecords => Set<StepRecord>();
    public DbSet<RequestDecision> RequestDecisions => Set<RequestDecision>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<OpenIssue> OpenIssues => Set<OpenIssue>();
    public DbSet<Meeting> Meetings => Set<Meeting>();
    public DbSet<Requirement> Requirements => Set<Requirement>();
    public DbSet<DocumentType> DocumentTypes => Set<DocumentType>();
    public DbSet<ProjectDocument> ProjectDocuments => Set<ProjectDocument>();
    public DbSet<Risk> Risks => Set<Risk>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<QuestionSet> QuestionSets => Set<QuestionSet>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<QuestionOption> QuestionOptions => Set<QuestionOption>();
    public DbSet<Questionnaire> Questionnaires => Set<Questionnaire>();
    public DbSet<QuestionAnswer> QuestionAnswers => Set<QuestionAnswer>();
    public DbSet<ProcessImprovement> ProcessImprovements => Set<ProcessImprovement>();
    public DbSet<CustomField> CustomFields => Set<CustomField>();
    public DbSet<AiUsage> AiUsages => Set<AiUsage>();

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

        ConfigureTenants(modelBuilder);

        // Records are never deleted (users are deactivated, history is kept),
        // so no relationship deletes its children.
        foreach (var foreignKey in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
        {
            foreignKey.DeleteBehavior = DeleteBehavior.Restrict;
        }
    }

    /// <summary>
    /// Every table except Tenants belongs to a company: a TenantId foreign key (indexed) and a global query filter, so
    /// services see only the signed-in user's company without changing. Rules: docs/multi-tenancy.md → Okuma kuralı.
    /// </summary>
    private void ConfigureTenants(ModelBuilder modelBuilder)
    {
        var tenantOwned = modelBuilder.Model.GetEntityTypes()
            .Select(e => e.ClrType)
            .Where(t => typeof(ITenantOwned).IsAssignableFrom(t))
            .ToList();
        var filter = typeof(IsdlcDbContext).GetMethod(nameof(ApplyTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

        foreach (var type in tenantOwned)
        {
            modelBuilder.Entity(type).HasOne(typeof(Tenant)).WithMany().HasForeignKey(nameof(ITenantOwned.TenantId));
            filter.MakeGenericMethod(type).Invoke(this, [modelBuilder]);
        }

        // A user's TenantId is empty only for the Platform Administrator. The null check matters: EF Core keeps C#'s
        // "null == null", so without it a context with no company would see the users that have none.
        modelBuilder.Entity<User>().HasOne<Tenant>().WithMany().HasForeignKey(u => u.TenantId);
        modelBuilder.Entity<User>().HasQueryFilter(u => CurrentTenantId != null && u.TenantId == CurrentTenantId);
    }

    private void ApplyTenantFilter<T>(ModelBuilder modelBuilder) where T : class, ITenantOwned =>
        modelBuilder.Entity<T>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
}
