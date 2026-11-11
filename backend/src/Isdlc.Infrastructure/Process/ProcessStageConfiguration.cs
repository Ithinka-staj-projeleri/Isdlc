using Isdlc.Domain.Process;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Isdlc.Infrastructure.Process;

public class ProcessStageConfiguration : IEntityTypeConfiguration<ProcessStage>
{
    public void Configure(EntityTypeBuilder<ProcessStage> builder)
    {
        builder.Property(x => x.Title).HasMaxLength(100);
        builder.Property(x => x.ShortTitle).HasMaxLength(50);

        builder.HasIndex(x => new { x.ProcessFlowId, x.Order }).IsUnique();

        builder.HasOne(x => x.ProcessFlow).WithMany(x => x.Stages).HasForeignKey(x => x.ProcessFlowId);
    }
}
