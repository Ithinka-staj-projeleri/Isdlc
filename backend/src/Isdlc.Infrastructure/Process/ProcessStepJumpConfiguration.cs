using Isdlc.Domain.Process;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Isdlc.Infrastructure.Process;

public class ProcessStepJumpConfiguration : IEntityTypeConfiguration<ProcessStepJump>
{
    public void Configure(EntityTypeBuilder<ProcessStepJump> builder)
    {
        builder.Property(x => x.Label).HasMaxLength(200);

        builder.HasIndex(x => new { x.ProcessStepId, x.TargetStepId }).IsUnique();

        builder.HasOne(x => x.ProcessStep).WithMany(x => x.NoJumps).HasForeignKey(x => x.ProcessStepId);
        builder.HasOne(x => x.TargetStep).WithMany().HasForeignKey(x => x.TargetStepId);
    }
}
