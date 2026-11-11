namespace Isdlc.Domain.Process;

/// <summary>
/// A stage ("bölüm") of a flow. Master data loaded from the seed; not edited by users.
/// </summary>
public class ProcessStage
{
    public int Id { get; set; }

    public int ProcessFlowId { get; set; }
    public ProcessFlow ProcessFlow { get; set; } = null!;

    /// <summary>Starts at 1 in each flow; unique within the flow.</summary>
    public int Order { get; set; }

    public string Title { get; set; } = string.Empty;
    public string ShortTitle { get; set; } = string.Empty;

    public ICollection<ProcessStep> Steps { get; set; } = new List<ProcessStep>();
}
