using Isdlc.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Isdlc.Infrastructure.Tenants;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.Property(x => x.Code).HasMaxLength(Tenant.CodeMaxLength);
        builder.Property(x => x.Name).HasMaxLength(Tenant.NameMaxLength);
        builder.Property(x => x.AiModel).HasMaxLength(Isdlc.Domain.Agents.AiUsage.MaxModelLength);
        builder.ToTable(t => t.HasCheckConstraint("CK_Tenants_AiMonthlyTokenLimit", "[AiMonthlyTokenLimit] > 0"));

        builder.HasIndex(x => x.Code).IsUnique();
    }
}
