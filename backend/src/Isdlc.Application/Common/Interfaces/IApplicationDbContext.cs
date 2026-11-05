using Isdlc.Domain.Audit;
using Isdlc.Domain.Identity;
using Isdlc.Domain.OpenIssues;
using Isdlc.Domain.Process;
using Isdlc.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Isdlc.Application.Common.Interfaces;

/// <summary>
/// Database access for Application services. Implemented by IsdlcDbContext in Infrastructure,
/// so services can query with LINQ without depending on the Infrastructure project.
/// When a module adds an entity, its DbSet is added here and in IsdlcDbContext.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Project> Projects { get; }
    DbSet<ProcessStage> ProcessStages { get; }
    DbSet<ProcessStep> ProcessSteps { get; }
    DbSet<ProcessStepJump> ProcessStepJumps { get; }
    DbSet<StepRecord> StepRecords { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<OpenIssue> OpenIssues { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Change tracking details of a loaded entity (e.g. to mark a property as modified).</summary>
    EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class;
}
