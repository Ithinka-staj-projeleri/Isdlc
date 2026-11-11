using Isdlc.Domain.Projects;

namespace Isdlc.Domain.Process;

/// <summary>
/// A company work flow (new software project, add-on, process improvement…). Master data loaded from
/// Seed/flows/*.json; not edited by users. Every record (Project) follows one flow.
/// Rules: docs/process-api.md → "Akışlar".
/// </summary>
public class ProcessFlow
{
    public int Id { get; set; }

    /// <summary>e.g. "new-software-project", unique. Seed files and code find a flow by this.</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Which menu lists the flow's records.</summary>
    public ProcessFlowKind Kind { get; set; }

    /// <summary>
    /// The stage from which a record counts as a project (Projeler) instead of a request (Talepler).
    /// Null: the record never becomes a project.
    /// </summary>
    public int? ProjectStageOrder { get; set; }

    public ICollection<ProcessStage> Stages { get; set; } = new List<ProcessStage>();

    public ICollection<Project> Projects { get; set; } = new List<Project>();
}
