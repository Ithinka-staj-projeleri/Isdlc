using Isdlc.Application.Common.Interfaces;
using Isdlc.Domain.Common;
using Isdlc.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Isdlc.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Writes the company on every new record and refuses a record of another company, right before each save.
/// Registered first, so the other interceptors and the audit log already see the company.
/// Rules: docs/multi-tenancy.md → Yazma kuralı.
/// </summary>
public class TenantInterceptor(ITenantContext tenant) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Apply(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries<ITenantOwned>())
        {
            Check(entry, entry.Entity.GetType().Name, entry.Entity.TenantId, id => entry.Entity.TenantId = id);
        }

        // A user is not ITenantOwned: the Platform Administrator belongs to no company. Every other user follows the
        // same rule as any other record.
        foreach (var entry in context.ChangeTracker.Entries<User>())
        {
            if (entry.Entity.IsPlatformAdmin)
            {
                CheckPlatformAdmin(entry);
                continue;
            }

            Check(entry, nameof(User), entry.Entity.TenantId ?? 0, id => entry.Entity.TenantId = id);
        }
    }

    /// <summary>The Platform Administrator stays without a company, whatever company the code works as.</summary>
    private static void CheckPlatformAdmin(EntityEntry<User> entry)
    {
        switch (entry.State)
        {
            case EntityState.Added:
                TenantRules.ForNewPlatformAdmin(entry.Entity.TenantId);
                break;

            case EntityState.Modified:
                var property = entry.Property(u => u.TenantId);
                TenantRules.ForChangedRecord(nameof(User), property.OriginalValue, property.CurrentValue);
                break;
        }
    }

    private void Check(EntityEntry entry, string entityName, int tenantId, Action<int> set)
    {
        switch (entry.State)
        {
            case EntityState.Added:
                var resolved = TenantRules.ForNewRecord(entityName, tenantId, tenant.TenantId);
                if (resolved != tenantId)
                {
                    set(resolved);
                }
                break;

            case EntityState.Modified:
                var property = entry.Property(nameof(ITenantOwned.TenantId));
                TenantRules.ForChangedRecord(entityName, property.OriginalValue as int?, property.CurrentValue as int?);
                break;
        }
    }
}

/// <summary>The write rules of TenantInterceptor, without a database (docs/multi-tenancy.md → Yazma kuralı).</summary>
public static class TenantRules
{
    /// <summary>
    /// The company a new record is saved with: the current one when the record has none (0). A record of another
    /// company, or any record while no company is known, is a programming error: never saved, logged as a 500.
    /// </summary>
    public static int ForNewRecord(string entityName, int tenantId, int? currentTenantId)
    {
        if (currentTenantId is null)
        {
            throw new InvalidOperationException(
                $"A new {entityName} cannot be saved without a company: nobody of a company is signed in.");
        }

        if (tenantId != 0 && tenantId != currentTenantId)
        {
            throw new InvalidOperationException(
                $"A new {entityName} of company {tenantId} cannot be saved while working as company {currentTenantId}.");
        }

        return currentTenantId.Value;
    }

    /// <summary>
    /// A new Platform Administrator: never of a company, even while the code works as one (the seed opens it after the
    /// companies). A company given to it is a programming error.
    /// </summary>
    public static void ForNewPlatformAdmin(int? tenantId)
    {
        if (tenantId is not null)
        {
            throw new InvalidOperationException(
                $"A Platform Administrator belongs to no company; company {tenantId} cannot be given.");
        }
    }

    /// <summary>A saved record never moves to another company.</summary>
    public static void ForChangedRecord(string entityName, int? originalTenantId, int? currentTenantId)
    {
        if (originalTenantId != currentTenantId)
        {
            throw new InvalidOperationException(
                $"The company of a saved {entityName} cannot change (from {originalTenantId} to {currentTenantId}).");
        }
    }
}
