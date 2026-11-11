namespace Isdlc.Domain.QuestionBank;

/// <summary>
/// One of the company's forms (level, request analysis, risk analysis, internal approval).
/// Reference data: loaded from Seed/question-sets/{code}.json, never changed by users, not audited.
/// Rules: docs/question-bank-api.md
/// </summary>
public class QuestionSet
{
    public const int BandsMaxLength = 4000;

    public int Id { get; set; }

    /// <summary>"level", "request-analysis", "risk-analysis", "internal-approval".</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>The form it comes from: "ISDLC-Form-Analiz-014".</summary>
    public string SourceDocument { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public AnswerMode AnswerMode { get; set; }

    /// <summary>
    /// The form's own thresholds as JSON (level ranges, risk degrees); read by the form's rule class, not here.
    /// Null for forms without thresholds.
    /// </summary>
    public string? Bands { get; set; }

    public List<Question> Questions { get; set; } = [];

    /// <summary>
    /// Every option of the set: the shared ones (QuestionId null) and the question-specific ones (level).
    /// </summary>
    public List<QuestionOption> Options { get; set; } = [];
}
