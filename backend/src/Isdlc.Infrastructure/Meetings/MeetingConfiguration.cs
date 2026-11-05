using Isdlc.Domain.Meetings;
using Isdlc.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Isdlc.Infrastructure.Meetings;

public class MeetingConfiguration : IEntityTypeConfiguration<Meeting>
{
    public void Configure(EntityTypeBuilder<Meeting> builder)
    {
        builder.Property(x => x.Code).HasMaxLength(20);
        builder.Property(x => x.Participants).HasMaxLength(1000);
        builder.Property(x => x.Agenda).HasMaxLength(4000);

        // Codes increase within a project; two meetings saved at the same time cannot get the same code.
        builder.HasIndex(x => new { x.ProjectId, x.Code }).IsUnique();

        builder.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId);

        builder.ConfigureAuditFields();
    }
}
