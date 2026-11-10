using Isdlc.Domain.Risks;
using Isdlc.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Isdlc.Infrastructure.Risks;

public class RiskConfiguration : IEntityTypeConfiguration<Risk>
{
    public void Configure(EntityTypeBuilder<Risk> builder)
    {
        // The database itself refuses a probability or impact outside 1–5, so a score is always 1–25.
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Risks_Probability", "[Probability] BETWEEN 1 AND 5");
            t.HasCheckConstraint("CK_Risks_Impact", "[Impact] BETWEEN 1 AND 5");
        });

        builder.Property(x => x.Code).HasMaxLength(20);
        builder.Property(x => x.Title).HasMaxLength(300);
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.Mitigation).HasMaxLength(2000);

        // Codes increase within a project; two records saved at the same time cannot get the same code.
        builder.HasIndex(x => new { x.ProjectId, x.Code }).IsUnique();
        builder.HasIndex(x => new { x.ProjectId, x.Status });

        builder.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId);
        builder.HasOne(x => x.OwnerUser).WithMany().HasForeignKey(x => x.OwnerUserId);

        builder.ConfigureAuditFields();
    }
}
