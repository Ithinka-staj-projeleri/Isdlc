using Isdlc.Domain.Common;

namespace Isdlc.Domain.Process;

/// <summary>
/// A stage ("bölüm") of a flow. Copied from the seed; the company administrator can edit it (Faz 7 · A7).
/// </summary>
public class ProcessStage : ITenantOwned
{
    public int Id { get; set; }

    public int TenantId { get; set; }

    public int ProcessFlowId { get; set; }
    public ProcessFlow ProcessFlow { get; set; } = null!;

    /// <summary>Starts at 1 in each flow; unique within the flow.</summary>
    public int Order { get; set; }

    public string Title { get; set; } = string.Empty;
    public string ShortTitle { get; set; } = string.Empty;

    /// <summary>Never deleted: a stage is deactivated once all its steps are (Faz 7 · A7).</summary>
    public bool IsActive { get; set; } = true;

    public ICollection<ProcessStep> Steps { get; set; } = new List<ProcessStep>();
}
