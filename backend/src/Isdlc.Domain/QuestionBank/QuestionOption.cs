namespace Isdlc.Domain.QuestionBank;

/// <summary>
/// A possible answer. Belongs to the whole set (QuestionId null: "Yanıtlandı", "Risk Var"…) or to one question
/// (level: five options, each with its own points). Reference data (seeded, not audited).
/// </summary>
public class QuestionOption
{
    public int Id { get; set; }

    public int QuestionSetId { get; set; }
    public QuestionSet QuestionSet { get; set; } = null!;

    /// <summary>Set for a question's own option; null for an option shared by the set.</summary>
    public int? QuestionId { get; set; }
    public Question? Question { get; set; }

    /// <summary>Permanent key: "{set}/{code}" for a shared option, "{question key}/{n}" for a question's option.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>The label exactly as on the form.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Points added to the section's sum (level).</summary>
    public int? Points { get; set; }

    /// <summary>What it means for counts and results; null for level options, which only have points.</summary>
    public OptionMeaning? Meaning { get; set; }

    /// <summary>Probability and severity are entered with this answer (risk analysis: "Risk Var", "Belirsiz").</summary>
    public bool IsScored { get; set; }

    /// <summary>The form expects an explanation with this answer; missing ones are counted, not refused.</summary>
    public bool RequiresNote { get; set; }

    public int Order { get; set; }
}
