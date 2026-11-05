namespace Isdlc.Domain.Projects;

public enum RequestSource
{
    Internal,
    External,
}

public enum RequestChannel
{
    Written,
    Verbal,
}

public enum RequestType
{
    NewProject,
    AdditionalModule,
    CompanyProduct,
    Support,
}

public enum ProjectPriority
{
    Low,
    Medium,
    High,
}

public enum ProjectStatus
{
    Active,
    Closed,
    Rejected,
    Lost,
    Redirected,
}
