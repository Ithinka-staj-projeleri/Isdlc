namespace Isdlc.Domain.Process;

/// <summary>
/// A step the flow can return to when a decision step is answered "No".
/// A decision step can have several options, e.g. 5.4 goes back to 4.1 or 4.9.
/// </summary>
public class ProcessStepJump
{
    public int Id { get; set; }

    /// <summary>The decision step.</summary>
    public int ProcessStepId { get; set; }
    public ProcessStep ProcessStep { get; set; } = null!;

    /// <summary>The step to return to.</summary>
    public int TargetStepId { get; set; }
    public ProcessStep TargetStep { get; set; } = null!;

    /// <summary>Option text shown to the user.</summary>
    public string Label { get; set; } = string.Empty;
}
