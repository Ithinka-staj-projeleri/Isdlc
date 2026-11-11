namespace Isdlc.Domain.Projects;

public enum RequestSource
{
    Internal,
    External,

    /// <summary>
    /// Group companies / related parties (İlişkili şirket). Added for the question bank's third subcategory;
    /// the request form and its labels come with D1 (docs/phase-4-plan.md).
    /// </summary>
    Affiliated,
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
