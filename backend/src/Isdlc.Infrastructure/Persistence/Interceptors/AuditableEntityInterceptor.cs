using Isdlc.Application.Common.Interfaces;
using Isdlc.Domain.Common;
using Isdlc.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Isdlc.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Fills the audit fields (CreatedAt/By, UpdatedAt/By) right before every save.
/// Values already set (e.g. by the seeder) are kept.
/// </summary>
public class AuditableEntityInterceptor(ICurrentUserService currentUser, TimeProvider timeProvider)
    : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        FillAuditFields(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        FillAuditFields(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void FillAuditFields(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;

        foreach (var entry in context.ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    if (entry.Entity.CreatedAt == default)
                    {
                        entry.Entity.CreatedAt = now;
                    }
                    if (entry.Entity.CreatedById == 0)
                    {
                        entry.Entity.CreatedById = RequireUserId();
                    }
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedById = RequireUserId();
                    // Creation info never changes after insert.
                    entry.Property(x => x.CreatedAt).IsModified = false;
                    entry.Property(x => x.CreatedById).IsModified = false;
                    break;
            }
        }

        // User is not an AuditableEntity but still records when it was created.
        foreach (var entry in context.ChangeTracker.Entries<User>())
        {
            if (entry.State == EntityState.Added && entry.Entity.CreatedAt == default)
            {
                entry.Entity.CreatedAt = now;
            }
        }
    }

    private int RequireUserId() =>
        currentUser.UserId ?? throw new InvalidOperationException("Cannot save changes without a current user.");
}
