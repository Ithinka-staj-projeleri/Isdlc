using Isdlc.Domain.Common;
using Isdlc.Domain.Projects;

namespace Isdlc.Domain.Process;

/// <summary>
/// A company work flow (new software project, add-on, process improvement…). Copied from Seed/flows/*.json; the company
/// administrator can edit its copy (Faz 7 · A7). Every record (Project) follows one flow.
/// Rules: docs/process-api.md → "Akışlar", docs/configurability.md → Akış düzenleme.
/// </summary>
public class ProcessFlow : ITenantOwned
{
    public int Id { get; set; }

    public int TenantId { get; set; }

    /// <summary>e.g. "new-software-project", unique in the company. Seed files find a flow by this; code by UsedFor.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>The template (Seed/ file code) this copy came from; empty for a record the company added itself.</summary>
    public string? TemplateCode { get; set; }

    /// <summary>The company changed this copy; the startup sync no longer follows the template (docs/multi-tenancy.md → Seed).</summary>
    public bool IsCustomized { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Position in flow lists (Süreç Kütüphanesi); the first flow is shown by default.</summary>
    public int Order { get; set; }

    /// <summary>Which menu lists the flow's records.</summary>
    public ProcessFlowKind Kind { get; set; }

    /// <summary>Which records this flow starts; null: none (Faz 7 · A7). At most one flow of the company per value.</summary>
    public ProcessFlowUse? UsedFor { get; set; }

    /// <summary>Catches two administrators editing the same flow at once (409).</summary>
    public byte[] RowVersion { get; set; } = [];

    /// <summary>
    /// The stage from which a record counts as a project (Projeler) instead of a request (Talepler).
    /// Null: the record never becomes a project.
    /// </summary>
    public int? ProjectStageOrder { get; set; }

    public ICollection<ProcessStage> Stages { get; set; } = new List<ProcessStage>();

    public ICollection<Project> Projects { get; set; } = new List<Project>();
}
