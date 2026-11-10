using Isdlc.Domain.Assessment;
using Isdlc.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Isdlc.Infrastructure.Assessment;

public class ProjectAssessmentConfiguration : IEntityTypeConfiguration<ProjectAssessment>
{
    private static readonly string[] LevelQuestions =
    [
        nameof(ProjectAssessment.Effort),
        nameof(ProjectAssessment.ScreensAndModules),
        nameof(ProjectAssessment.Integrations),
        nameof(ProjectAssessment.UserRoles),
        nameof(ProjectAssessment.DataSensitivity),
        nameof(ProjectAssessment.HardwareDependency),
    ];

    private static readonly string[] AcceptanceCriteria =
    [
        nameof(ProjectAssessment.StrategicFit),
        nameof(ProjectAssessment.CommercialValue),
        nameof(ProjectAssessment.TechnicalFeasibility),
        nameof(ProjectAssessment.ResourceAndSchedule),
        nameof(ProjectAssessment.CustomerFit),
        nameof(ProjectAssessment.RiskLevel),
    ];

    public void Configure(EntityTypeBuilder<ProjectAssessment> builder)
    {
        // The database itself refuses an answer outside its range; null (not answered yet) is allowed.
        builder.ToTable(t =>
        {
            foreach (var column in LevelQuestions)
            {
                t.HasCheckConstraint($"CK_ProjectAssessments_{column}", $"[{column}] IS NULL OR [{column}] BETWEEN 1 AND 3");
            }

            foreach (var column in AcceptanceCriteria)
            {
                t.HasCheckConstraint($"CK_ProjectAssessments_{column}", $"[{column}] IS NULL OR [{column}] BETWEEN 1 AND 5");
            }
        });

        // One assessment per project.
        builder.HasIndex(x => x.ProjectId).IsUnique();

        builder.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId);

        builder.ConfigureAuditFields();
    }
}
