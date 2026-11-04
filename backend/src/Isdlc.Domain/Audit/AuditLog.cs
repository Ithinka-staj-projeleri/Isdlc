using Isdlc.Domain.Identity;
using Isdlc.Domain.Projects;

namespace Isdlc.Domain.Audit;

/// <summary>
/// One change to an audited record. Written automatically during SaveChanges; never edited or deleted.
/// </summary>
public class AuditLog
{
    public long Id { get; set; }

    /// <summary>Entity type name, e.g. "Project", "Risk".</summary>
    public string EntityName { get; set; } = string.Empty;

    public int EntityId { get; set; }

    /// <summary>The project the record belongs to; used to list changes per project.</summary>
    public int? ProjectId { get; set; }
    public Project? Project { get; set; }

    public AuditAction Action { get; set; }

    /// <summary>
    /// JSON with the changed fields: {"Status": {"old": "Active", "new": "Lost"}}.
    /// Field names are the C# property names; the screen shows their Turkish labels.
    /// </summary>
    public string Changes { get; set; } = "{}";

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
}
