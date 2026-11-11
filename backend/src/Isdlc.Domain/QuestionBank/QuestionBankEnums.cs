namespace Isdlc.Domain.QuestionBank;

/// <summary>Where a set's options live (docs/question-bank-api.md → Kavramlar).</summary>
public enum AnswerMode
{
    /// <summary>One list of options for the whole set (request analysis, risk analysis, internal approval).</summary>
    SharedOptions,

    /// <summary>Every question has its own options with points (level).</summary>
    QuestionOptions,
}

public enum QuestionRequirement
{
    /// <summary>Zorunlu: the form is complete only when it has an answer.</summary>
    Required,

    /// <summary>Duruma bağlı: closed with "not applicable" when it does not fit the project.</summary>
    Conditional,
}

/// <summary>
/// What an option means, whatever its label on the form ("Yanıtlandı", "Uygun", "Risk Yok" are all Complete).
/// Calculations and the screen look at this, not at the label.
/// </summary>
public enum OptionMeaning
{
    Complete,
    Partial,
    Open,
    Negative,
    NotApplicable,
}
