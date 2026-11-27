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

        builder.Property(x => x.TemplateCode).HasMaxLength(50);

        // Every company has its own copy of the templates (docs/multi-tenancy.md → Benzersizlik).
        builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();

        // One flow per use in a company; flows without a use are not counted (Faz 7 · A7).
        builder.HasIndex(x => new { x.TenantId, x.UsedFor }).IsUnique().HasFilter("[UsedFor] IS NOT NULL");

        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}
