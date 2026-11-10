using Isdlc.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Isdlc.Infrastructure.Notifications;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.Property(x => x.Message).HasMaxLength(Notification.MessageMaxLength);

        // The bell asks "how many unread" every minute, and the list shows a user's newest first.
        builder.HasIndex(x => new { x.UserId, x.ReadAt });
        builder.HasIndex(x => new { x.UserId, x.CreatedAt });

        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
        builder.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId);
    }
}
