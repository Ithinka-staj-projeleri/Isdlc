using Isdlc.Domain.Common;
using Isdlc.Domain.Identity;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Isdlc.Infrastructure.Persistence;

public static class AuditableEntityConfigurationExtensions
{
    /// <summary>
    /// Links CreatedById / UpdatedById to Users. Call from every AuditableEntity configuration.
    /// </summary>
    public static void ConfigureAuditFields<T>(this EntityTypeBuilder<T> builder) where T : AuditableEntity
    {
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedById);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UpdatedById);
    }
}
