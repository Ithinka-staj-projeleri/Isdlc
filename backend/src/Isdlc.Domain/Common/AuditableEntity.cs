namespace Isdlc.Domain.Common;

/// <summary>
/// Base class for transactional entities. The audit fields are filled automatically
/// during SaveChanges; do not set them in application code.
/// </summary>
public abstract class AuditableEntity
{
    public DateTime CreatedAt { get; set; }
    public int CreatedById { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedById { get; set; }
}
