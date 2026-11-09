namespace Isdlc.Domain.Documents;

public enum DocumentStatus
{
    NotStarted,
    InProgress,
    InReview,
    Approved,

    /// <summary>The project does not need this document.</summary>
    NotRequired,
}
