using Isdlc.Domain.Process;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Isdlc.Infrastructure.Process;

public class ProcessStepConfiguration : IEntityTypeConfiguration<ProcessStep>
{
    public void Configure(EntityTypeBuilder<ProcessStep> builder)
    {
        builder.Property(x => x.Code).HasMaxLength(10);
        builder.Property(x => x.Text).HasMaxLength(500);
        builder.Property(x => x.NoBranchText).HasMaxLength(500);

        // Codes and orders repeat across flows ("1" exists in every flow), so they are unique per flow.
        builder.HasIndex(x => new { x.ProcessFlowId, x.Code }).IsUnique();
        builder.HasIndex(x => new { x.ProcessFlowId, x.Order }).IsUnique();

        builder.HasOne(x => x.ProcessStage).WithMany(x => x.Steps).HasForeignKey(x => x.ProcessStageId);
        builder.HasOne(x => x.ProcessFlow).WithMany().HasForeignKey(x => x.ProcessFlowId);
    }
}
