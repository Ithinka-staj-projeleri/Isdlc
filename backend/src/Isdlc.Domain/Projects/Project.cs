using Isdlc.Domain.Common;
using Isdlc.Domain.Identity;
using Isdlc.Domain.Process;

namespace Isdlc.Domain.Projects;

/// <summary>
/// A request and the project it turns into are the same record.
/// </summary>
public class Project : AuditableEntity, IAudited
{
    public int Id { get; set; }

    /// <summary>Shown to users, e.g. "ISDLC-2026-014". Generated automatically.</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public RequestSource Source { get; set; }
    public RequestChannel RequestChannel { get; set; }
    public RequestType RequestType { get; set; }

    /// <summary>Customer or internal unit name (no separate table in the MVP).</summary>
    public string CustomerName { get; set; } = string.Empty;

    /// <summary>Requester person or title. Not a system user.</summary>
    public string RequesterName { get; set; } = string.Empty;

    public bool RequiresNda { get; set; }

    public int? BusinessAnalystId { get; set; }
    public User? BusinessAnalyst { get; set; }

    public int? ProjectManagerId { get; set; }
    public User? ProjectManager { get; set; }

    /// <summary>1–3. Empty until the level is assessed.</summary>
    public int? Level { get; set; }

    /// <summary>Set when the project is accepted (step 3.2).</summary>
    public ProjectPriority? Priority { get; set; }

    public ProjectStatus Status { get; set; } = ProjectStatus.Active;

    /// <summary>The flow the record follows. Set when the record is opened; never changes.</summary>
    public int ProcessFlowId { get; set; }
    public ProcessFlow ProcessFlow { get; set; } = null!;

    /// <summary>Whether an offer will be prepared. Steps with RequiresOffer are skipped when false.</summary>
    public bool RequiresOffer { get; set; }

    /// <summary>Empty when the project is not active. Always a step of the record's own flow.</summary>
    public int? CurrentStepId { get; set; }
    public ProcessStep? CurrentStep { get; set; }

    public DateOnly StartDate { get; set; }
    public DateOnly? DueDate { get; set; }

    /// <summary>Concurrency token: a stale update fails instead of overwriting.</summary>
    public byte[] RowVersion { get; set; } = [];

    public ICollection<StepRecord> StepRecords { get; set; } = new List<StepRecord>();
}
