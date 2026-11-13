using Isdlc.Domain.Projects;
using Isdlc.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Isdlc.Infrastructure.Projects;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Projects_Level", "[Level] BETWEEN 1 AND 3");
            t.HasCheckConstraint("CK_Projects_ResearchDays", "[ResearchDays] BETWEEN 1 AND 30");
        });

        builder.Property(x => x.Code).HasMaxLength(20);
        builder.Property(x => x.Name).HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.CustomerName).HasMaxLength(200);
        builder.Property(x => x.RequesterName).HasMaxLength(200);
        builder.Property(x => x.RequesterContact).HasMaxLength(200);
        builder.Property(x => x.RequestChannelOther).HasMaxLength(200);
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => x.Status);

        builder.HasOne(x => x.BusinessAnalyst).WithMany().HasForeignKey(x => x.BusinessAnalystId);
        builder.HasOne(x => x.ProjectManager).WithMany().HasForeignKey(x => x.ProjectManagerId);
        builder.HasOne(x => x.CurrentStep).WithMany().HasForeignKey(x => x.CurrentStepId);
        builder.HasOne(x => x.ProcessFlow).WithMany(x => x.Projects).HasForeignKey(x => x.ProcessFlowId);

        builder.ConfigureAuditFields();
    }
}
