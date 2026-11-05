namespace Isdlc.Domain.OpenIssues;

public enum OpenIssueType
{
    Question,
    Action,
    Decision,
    ChangeRequest,
    Dependency,
    Assumption,
}

public enum OpenIssueStatus
{
    Open,
    InProgress,
    Closed,
}
