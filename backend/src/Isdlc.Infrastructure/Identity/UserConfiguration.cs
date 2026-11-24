using Isdlc.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Isdlc.Infrastructure.Identity;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(x => x.FullName).HasMaxLength(100);
        builder.Property(x => x.Email).HasMaxLength(256);
        builder.Property(x => x.PasswordHash).HasMaxLength(500);
        builder.Property(x => x.Expertise).HasMaxLength(UserProfileLimits.Expertise);
        builder.Property(x => x.WorkingNote).HasMaxLength(UserProfileLimits.WorkingNote);

        builder.HasIndex(x => x.Email).IsUnique();

        // Only the Platform Administrator belongs to no company, and never manages one (docs/multi-tenancy.md → Users).
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_Users_TenantByRole",
            $"([Role] = '{nameof(UserRole.PlatformAdmin)}' AND [TenantId] IS NULL AND [IsTenantAdmin] = 0)"
            + $" OR ([Role] <> '{nameof(UserRole.PlatformAdmin)}' AND [TenantId] IS NOT NULL)"));
    }
}
