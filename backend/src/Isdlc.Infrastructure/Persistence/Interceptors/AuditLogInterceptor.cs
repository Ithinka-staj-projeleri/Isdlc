using System.Text.Json;
using System.Text.Json.Serialization;
using Isdlc.Domain.Audit;
using Isdlc.Domain.Common;
using Isdlc.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace Isdlc.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Writes an AuditLog row for every change to an IAudited entity.
/// Two phases, because new records get their Id only when they are saved:
///   1. SavingChanges: capture which fields changed (old values are lost after the save).
///   2. SavedChanges: Ids are known, so write the log rows with a second save.
/// Both saves run in one transaction, so data is never saved without its log.
/// Must be registered after AuditableEntityInterceptor (it reads CreatedById / UpdatedById).
/// </summary>
public class AuditLogInterceptor(TimeProvider timeProvider) : SaveChangesInterceptor
{
    /// <summary>Never written to the log: secrets, technical fields, and fields already on the log row.</summary>
    private static readonly HashSet<string> IgnoredProperties =
    [
        "Id", "PasswordHash", "RowVersion",
        nameof(AuditableEntity.CreatedAt), nameof(AuditableEntity.CreatedById),
        nameof(AuditableEntity.UpdatedAt), nameof(AuditableEntity.UpdatedById),
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private List<PendingChange> _pending = [];
    private IDbContextTransaction? _ownTransaction;
    private bool _isWritingLogs;

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (CaptureChanges(eventData.Context) && eventData.Context!.Database.CurrentTransaction is null)
        {
            _ownTransaction = eventData.Context.Database.BeginTransaction();
        }
        return result;
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (CaptureChanges(eventData.Context) && eventData.Context!.Database.CurrentTransaction is null)
        {
            _ownTransaction = await eventData.Context.Database.BeginTransactionAsync(cancellationToken);
        }
        return result;
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        try
        {
            if (AddLogRows(eventData.Context))
            {
                WriteLogs(() => eventData.Context!.SaveChanges());
                _ownTransaction?.Commit();
            }
        }
        finally
        {
            // Without a commit, disposing rolls back: the data is not kept without its log.
            DisposeTransaction();
        }
        return result;
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        try
        {
            if (AddLogRows(eventData.Context))
            {
                await WriteLogsAsync(() => eventData.Context!.SaveChangesAsync(cancellationToken));
                if (_ownTransaction is not null)
                {
                    await _ownTransaction.CommitAsync(cancellationToken);
                }
            }
        }
        finally
        {
            // Without a commit, disposing rolls back: the data is not kept without its log.
            DisposeTransaction();
        }
        return result;
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        // Rolls back the data change as well; the original exception continues to the caller.
        _pending = [];
        DisposeTransaction();
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        SaveChangesFailed(eventData);
        return Task.CompletedTask;
    }

    /// <summary>Phase 1: remember what changed. Returns true when there is something to log.</summary>
    private bool CaptureChanges(DbContext? context)
    {
        if (context is null || _isWritingLogs)
        {
            return false;
        }

        _pending = context.ChangeTracker.Entries<AuditableEntity>()
            .Where(e => e.Entity is IAudited)
            .Select(Capture)
            .OfType<PendingChange>()
            .ToList();

        return _pending.Count > 0;
    }

    private static PendingChange? Capture(EntityEntry<AuditableEntity> entry)
    {
        AuditAction action;
        Func<PropertyEntry, bool> isLogged;
        switch (entry.State)
        {
            case EntityState.Added:
                action = AuditAction.Created;
                isLogged = p => p.CurrentValue is not null;
                break;
            case EntityState.Modified:
                action = AuditAction.Updated;
                isLogged = p => p.IsModified && !Equals(p.OriginalValue, p.CurrentValue);
                break;
            case EntityState.Deleted:
                action = AuditAction.Deleted;
                isLogged = p => p.OriginalValue is not null;
                break;
            default:
                return null;
        }

        var changes = entry.Properties
            .Where(p => !IgnoredProperties.Contains(p.Metadata.Name) && isLogged(p))
            .ToDictionary(
                p => p.Metadata.Name,
                p => new FieldChange(
                    action == AuditAction.Created ? null : p.OriginalValue,
                    action == AuditAction.Deleted ? null : p.CurrentValue));

        // Nothing really changed (e.g. a value was set to the same value).
        if (changes.Count == 0)
        {
            return null;
        }

        return new PendingChange(entry, action, changes);
    }

    /// <summary>Phase 2: build the log rows now that Ids are known. Returns true when rows were added.</summary>
    private bool AddLogRows(DbContext? context)
    {
        if (context is null || _isWritingLogs || _pending.Count == 0)
        {
            return false;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        foreach (var change in _pending)
        {
            var entity = change.Entry.Entity;
            var entityId = (int)change.Entry.Property("Id").CurrentValue!;

            // New values are read again now: a foreign key to a record added in the same save (e.g. an open issue's
            // SourceMeetingId when the minutes are saved with it) held a temporary negative id before the save.
            if (change.Action != AuditAction.Deleted)
            {
                foreach (var name in change.Fields.Keys.ToList())
                {
                    change.Fields[name] = change.Fields[name] with { New = change.Entry.Property(name).CurrentValue };
                }
            }

            context.Set<AuditLog>().Add(new AuditLog
            {
                EntityName = entity.GetType().Name,
                EntityId = entityId,
                ProjectId = entity switch
                {
                    Project => entityId,
                    IProjectRecord record => record.ProjectId,
                    _ => null,
                },
                Action = change.Action,
                Changes = JsonSerializer.Serialize(change.Fields, JsonOptions),
                // Who and when come from the record's own audit fields, so the log and the record always agree.
                UserId = change.Action == AuditAction.Created ? entity.CreatedById : entity.UpdatedById ?? entity.CreatedById,
                CreatedAt = change.Action == AuditAction.Created ? entity.CreatedAt : entity.UpdatedAt ?? now,
            });
        }

        _pending = [];
        return true;
    }

    private void WriteLogs(Action save)
    {
        _isWritingLogs = true;
        try { save(); }
        finally { _isWritingLogs = false; }
    }

    private async Task WriteLogsAsync(Func<Task> save)
    {
        _isWritingLogs = true;
        try { await save(); }
        finally { _isWritingLogs = false; }
    }

    private void DisposeTransaction()
    {
        if (_isWritingLogs)
        {
            return;
        }
        _ownTransaction?.Dispose();
        _ownTransaction = null;
    }

    private sealed record PendingChange(
        EntityEntry<AuditableEntity> Entry, AuditAction Action, Dictionary<string, FieldChange> Fields);

    private sealed record FieldChange(
        [property: JsonPropertyName("old")] object? Old,
        [property: JsonPropertyName("new")] object? New);
}
