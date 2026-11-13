using Isdlc.Domain.ProcessImprovements;
using Isdlc.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Isdlc.Infrastructure.ProcessImprovements;

public class ProcessImprovementConfiguration : IEntityTypeConfiguration<ProcessImprovement>
{
    public void Configure(EntityTypeBuilder<ProcessImprovement> builder)
    {
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_ProcessImprovements_Impact", "[Impact] IS NULL OR [Impact] BETWEEN 1 AND 5");
            t.HasCheckConstraint("CK_ProcessImprovements_Frequency", "[Frequency] IS NULL OR [Frequency] BETWEEN 1 AND 5");
            t.HasCheckConstraint("CK_ProcessImprovements_Ease", "[Ease] IS NULL OR [Ease] BETWEEN 1 AND 5");
            // The three scores are given together or not at all.
            t.HasCheckConstraint("CK_ProcessImprovements_ScoresTogether",
                "([Impact] IS NULL AND [Frequency] IS NULL AND [Ease] IS NULL) "
                + "OR ([Impact] IS NOT NULL AND [Frequency] IS NOT NULL AND [Ease] IS NOT NULL)");
        });

        builder.Property(x => x.AffectedProcess).HasMaxLength(ProcessImprovement.MaxShortText);
        builder.Property(x => x.SuggestedBy).HasMaxLength(ProcessImprovement.MaxShortText);
        builder.Property(x => x.ProposedSolution).HasMaxLength(ProcessImprovement.MaxProposedSolution);
        builder.Property(x => x.CurrentState).HasMaxLength(ProcessImprovement.MaxStudyText);
        builder.Property(x => x.RootCause).HasMaxLength(ProcessImprovement.MaxStudyText);
        builder.Property(x => x.Solution).HasMaxLength(ProcessImprovement.MaxStudyText);
        builder.Property(x => x.PilotPlan).HasMaxLength(ProcessImprovement.MaxStudyText);
        builder.Property(x => x.CostBenefit).HasMaxLength(ProcessImprovement.MaxStudyText);
        builder.Property(x => x.PilotTarget).HasMaxLength(ProcessImprovement.MaxPilotMeasure);
        builder.Property(x => x.PilotActual).HasMaxLength(ProcessImprovement.MaxPilotMeasure);
        builder.Property(x => x.PilotFeedback).HasMaxLength(ProcessImprovement.MaxPilotFeedback);
        builder.Property(x => x.PilotDecision).HasMaxLength(ProcessImprovement.MaxPilotDecision);

        // One row per proposal.
        builder.HasIndex(x => x.ProjectId).IsUnique();
        builder.HasOne(x => x.Project).WithOne().HasForeignKey<ProcessImprovement>(x => x.ProjectId);

        builder.ConfigureAuditFields();
    }
}
