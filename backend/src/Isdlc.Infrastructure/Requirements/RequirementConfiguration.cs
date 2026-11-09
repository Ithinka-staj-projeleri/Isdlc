using Isdlc.Domain.Requirements;
using Isdlc.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Isdlc.Infrastructure.Requirements;

public class RequirementConfiguration : IEntityTypeConfiguration<Requirement>
{
    public void Configure(EntityTypeBuilder<Requirement> builder)
    {
        builder.Property(x => x.Code).HasMaxLength(20);
        builder.Property(x => x.Title).HasMaxLength(300);
        builder.Property(x => x.Description).HasMaxLength(4000);
        builder.Property(x => x.AcceptanceCriteria).HasMaxLength(4000);

        // Codes increase within a project; two records saved at the same time cannot get the same code.
        builder.HasIndex(x => new { x.ProjectId, x.Code }).IsUnique();

        builder.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId);

        builder.ConfigureAuditFields();
    }
}
