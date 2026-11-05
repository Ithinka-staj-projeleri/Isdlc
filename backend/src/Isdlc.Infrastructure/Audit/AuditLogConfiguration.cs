using Isdlc.Domain.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Isdlc.Infrastructure.Audit;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.Property(x => x.EntityName).HasMaxLength(100);
        // Changes is JSON of varying size, so it stays nvarchar(max).

        // The Audit screen lists changes per project or per record, newest first.
        builder.HasIndex(x => new { x.ProjectId, x.CreatedAt });
        builder.HasIndex(x => new { x.EntityName, x.EntityId });

        builder.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
    }
}
