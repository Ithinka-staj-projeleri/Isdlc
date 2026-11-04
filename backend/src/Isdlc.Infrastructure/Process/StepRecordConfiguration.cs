using Isdlc.Domain.Process;
using Isdlc.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Isdlc.Infrastructure.Process;

public class StepRecordConfiguration : IEntityTypeConfiguration<StepRecord>
{
    public void Configure(EntityTypeBuilder<StepRecord> builder)
    {
        builder.Property(x => x.Note).HasMaxLength(1000);

        // Process history is read per project, newest first.
        builder.HasIndex(x => new { x.ProjectId, x.PerformedAt });

        builder.HasOne(x => x.Project).WithMany(x => x.StepRecords).HasForeignKey(x => x.ProjectId);
        builder.HasOne(x => x.ProcessStep).WithMany().HasForeignKey(x => x.ProcessStepId);
        builder.HasOne(x => x.JumpedToStep).WithMany().HasForeignKey(x => x.JumpedToStepId);
        builder.HasOne(x => x.PerformedBy).WithMany().HasForeignKey(x => x.PerformedById);

        builder.ConfigureAuditFields();
    }
}
