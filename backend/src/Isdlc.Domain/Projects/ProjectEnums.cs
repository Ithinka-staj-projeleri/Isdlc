namespace Isdlc.Domain.Projects;

public enum RequestSource
{
    Internal,
    External,

    /// <summary>Group companies / related parties (İlişkili şirket).</summary>
    Affiliated,
}

/// <summary>How the request came in. The company form has only e-mail and phone.</summary>
public enum RequestChannel
{
    Email,
    Phone,
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
