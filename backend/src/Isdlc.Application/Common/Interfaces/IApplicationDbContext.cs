using Isdlc.Domain.Assessment;
using Isdlc.Domain.Audit;
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
    DbSet<ProcessFlow> ProcessFlows { get; }
    DbSet<ProcessStage> ProcessStages { get; }
    DbSet<ProcessStep> ProcessSteps { get; }
    DbSet<ProcessStepJump> ProcessStepJumps { get; }
    DbSet<StepRecord> StepRecords { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<OpenIssue> OpenIssues { get; }
    DbSet<Meeting> Meetings { get; }
    DbSet<Requirement> Requirements { get; }
    DbSet<DocumentType> DocumentTypes { get; }
    DbSet<ProjectDocument> ProjectDocuments { get; }
    DbSet<Risk> Risks { get; }
    DbSet<ProjectAssessment> ProjectAssessments { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<QuestionSet> QuestionSets { get; }
    DbSet<Question> Questions { get; }
    DbSet<QuestionOption> QuestionOptions { get; }
    DbSet<Questionnaire> Questionnaires { get; }
    DbSet<QuestionAnswer> QuestionAnswers { get; }
    DbSet<ProcessImprovement> ProcessImprovements { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Change tracking details of a loaded entity (e.g. to mark a property as modified).</summary>
    EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class;
}
