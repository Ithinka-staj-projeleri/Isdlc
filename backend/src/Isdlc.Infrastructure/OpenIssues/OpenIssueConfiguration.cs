using Isdlc.Domain.OpenIssues;
using Isdlc.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Isdlc.Infrastructure.OpenIssues;

public class OpenIssueConfiguration : IEntityTypeConfiguration<OpenIssue>
{
    public void Configure(EntityTypeBuilder<OpenIssue> builder)
    {
        // The database itself refuses a record with no owner or with both owners.
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_OpenIssues_Owner",
            "([OwnerUserId] IS NULL AND [OwnerName] IS NOT NULL) OR ([OwnerUserId] IS NOT NULL AND [OwnerName] IS NULL)"));

        builder.Property(x => x.Code).HasMaxLength(20);
        builder.Property(x => x.Title).HasMaxLength(300);
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.OwnerName).HasMaxLength(200);

        // Codes increase within a project; two records saved at the same time cannot get the same code.
        builder.HasIndex(x => new { x.ProjectId, x.Code }).IsUnique();
        builder.HasIndex(x => new { x.ProjectId, x.Status });

        builder.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId);
        builder.HasOne(x => x.OwnerUser).WithMany().HasForeignKey(x => x.OwnerUserId);

        builder.ConfigureAuditFields();
    }
}
