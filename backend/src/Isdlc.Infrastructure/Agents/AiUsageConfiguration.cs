using Isdlc.Domain.Agents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Isdlc.Infrastructure.Agents;

public class AiUsageConfiguration : IEntityTypeConfiguration<AiUsage>
{
    public void Configure(EntityTypeBuilder<AiUsage> builder)
    {
        builder.Property(x => x.Model).HasMaxLength(AiUsage.MaxModelLength);

        // The month's usage and the monthly limit read one company's rows by date.
        builder.HasIndex(x => new { x.TenantId, x.CreatedAt });

        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
        builder.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId);
    }
}
