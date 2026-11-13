namespace Isdlc.Domain.Process;

public enum StepAction
{
    Completed,
    Yes,
    No,

    /// <summary>Request rejected by the Software Department Manager.</summary>
    Rejected,

    /// <summary>Request postponed by the Software Department Manager; it waits on the same step.</summary>
    Postponed,

    /// <summary>A postponed request taken back for evaluation; it continues on the same step.</summary>
    Resumed,
}
