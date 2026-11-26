using Isdlc.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Isdlc.Infrastructure.Identity;

/// <summary>A user's permissions (Faz 7 · A2). The company filter and the Tenant key come from ITenantOwned.</summary>
public class UserPermissionConfiguration : IEntityTypeConfiguration<UserPermission>
{
    public void Configure(EntityTypeBuilder<UserPermission> builder)
    {
        builder.HasOne(x => x.User).WithMany(u => u.Permissions).HasForeignKey(x => x.UserId);

        builder.HasIndex(x => new { x.UserId, x.Permission }).IsUnique();
    }
}
