using Isdlc.Domain.Projects;
using Isdlc.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Isdlc.Infrastructure.Projects;

public class RequestDecisionConfiguration : IEntityTypeConfiguration<RequestDecision>
{
    public void Configure(EntityTypeBuilder<RequestDecision> builder)
    {
        builder.Property(x => x.DecidedBy).HasMaxLength(200);
        builder.Property(x => x.ReasonOther).HasMaxLength(200);
        builder.Property(x => x.Explanation).HasMaxLength(2000);

        // The chosen reasons as a JSON list of names: ["BudgetConstraint","Prioritization"].
        builder.PrimitiveCollection(x => x.Reasons)
            .HasMaxLength(500)
            .ElementType(element => element.HasConversion<string>());

        // Decisions are read per project, newest first.
        builder.HasIndex(x => new { x.ProjectId, x.DecisionDate });
        builder.HasIndex(x => x.StepRecordId).IsUnique();

        builder.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId);
        builder.HasOne(x => x.StepRecord).WithMany().HasForeignKey(x => x.StepRecordId);

        builder.ConfigureAuditFields();
    }
}
