namespace Isdlc.Domain.Process;

/// <summary>
/// One of the 13 ISDLC stages. Master data loaded from the seed; not edited by users.
/// </summary>
public class ProcessStage
{
    public int Id { get; set; }

    /// <summary>1–13, unique.</summary>
    public int Order { get; set; }

    public string Title { get; set; } = string.Empty;
    public string ShortTitle { get; set; } = string.Empty;

    public ICollection<ProcessStep> Steps { get; set; } = new List<ProcessStep>();
}
