using Isdlc.Domain.Process;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Isdlc.Infrastructure.Process;

public class ProcessFlowConfiguration : IEntityTypeConfiguration<ProcessFlow>
{
    public void Configure(EntityTypeBuilder<ProcessFlow> builder)
    {
        builder.Property(x => x.Code).HasMaxLength(50);
        builder.Property(x => x.Name).HasMaxLength(100);

        builder.HasIndex(x => x.Code).IsUnique();
    }
}
