namespace Isdlc.Domain.Assessment;

/// <summary>
/// What the customer fit survey (form 006) suggests; never stored, calculated from the answers.
/// The thresholds are in Seed/question-sets/customer-fit.json (bands). The decision is the analyst's at step 4.
/// </summary>
public enum CustomerFitRating
{
    /// <summary>Uygun: 85–100.</summary>
    Fit,

    /// <summary>Şartlı uygun: 70–84.</summary>
    ConditionallyFit,

    /// <summary>Riskli: 50–69.</summary>
    Risky,

    /// <summary>Uygun değil: 0–49.</summary>
    NotFit,
}
