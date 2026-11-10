using Isdlc.Application.Common.Models;
using Isdlc.Domain.Audit;

namespace Isdlc.Application.Audit.Dtos;

/// <summary>
/// One change to a project's records (GET /api/v1/projects/{projectId}/audit-logs).
/// EntityType and Field are C# names ("OpenIssue", "Status"); the screen shows their Turkish labels.
/// </summary>
public record AuditLogDto(
    long Id,
    string EntityType,
    int EntityId,
    string? RecordCode, // "AK-003", "TT-002", the project code, or the document type's name; null if not found
    AuditAction Action,
    IReadOnlyList<AuditFieldChangeDto> Changes,
    UserSummaryDto PerformedBy,
    DateTime PerformedAt);

/// <summary>
/// One field of a change. Values are text: enum values as their names ("Closed"), dates as "2026-11-12",
/// and references already readable (a user id becomes the user's name, a step id the step code).
/// Null means the field was empty (or, for a new record, that there was no old value).
/// </summary>
public record AuditFieldChangeDto(string Field, string? OldValue, string? NewValue);

/// <summary>List filter: ?entityType=OpenIssue (Project, OpenIssue, Meeting, Requirement, ProjectDocument).</summary>
public record AuditLogFilter
{
    public string? EntityType { get; init; }
}
