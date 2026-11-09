namespace Isdlc.Domain.Requirements;

public enum RequirementType
{
    Business,
    Functional,
    NonFunctional,
}

/// <summary>MoSCoW.</summary>
public enum RequirementPriority
{
    MustHave,
    ShouldHave,
    CouldHave,
    WontHave,
}

public enum RequirementStatus
{
    Draft,
    InReview,
    Approved,
    Cancelled,
}
