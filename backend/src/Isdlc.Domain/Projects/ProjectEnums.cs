namespace Isdlc.Domain.Projects;

public enum RequestSource
{
    Internal,
    External,

    /// <summary>
    /// Group companies / related parties (İlişkili şirket). The question bank's third subcategory;
    /// not an option on the company's request form, so the request form does not offer it.
    /// </summary>
    Affiliated,
}

/// <summary>How the request came in: the options of the company's Talep Kayıt Formu.</summary>
public enum RequestChannel
{
    Email,
    Phone,
    Meeting,

    /// <summary>"Diğer": the channel is described in Project.RequestChannelOther.</summary>
    Other,
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

    /// <summary>
    /// Ertelendi: a request put on hold by the Software Department Manager (Talep Ret / Erteleme Formu). It keeps its
    /// current step and continues from there when it is taken back; nothing can be done on it meanwhile.
    /// </summary>
    Postponed,
}
