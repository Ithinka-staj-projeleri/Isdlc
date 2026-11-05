namespace Isdlc.Domain.Process;

public enum StepAction
{
    Completed,
    Yes,
    No,

    /// <summary>Request rejected by the Software Department Manager.</summary>
    Rejected,
}
