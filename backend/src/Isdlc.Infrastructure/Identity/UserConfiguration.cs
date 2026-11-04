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

        builder.HasIndex(x => x.Email).IsUnique();
    }
}
