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

        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => x.Order).IsUnique();

        builder.HasOne(x => x.ProcessStage).WithMany(x => x.Steps).HasForeignKey(x => x.ProcessStageId);
    }
}
