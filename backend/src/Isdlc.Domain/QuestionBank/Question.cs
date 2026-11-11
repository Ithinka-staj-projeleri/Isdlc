using Isdlc.Domain.Projects;

namespace Isdlc.Domain.QuestionBank;

/// <summary>
/// One row of a form's "Sorular" sheet. Reference data (seeded, not audited). A question that leaves the seed file
/// is not deleted but made inactive, so its answers and their history stay.
/// </summary>
public class Question
{
    public int Id { get; set; }

    public int QuestionSetId { get; set; }
    public QuestionSet QuestionSet { get; set; } = null!;

    /// <summary>
    /// Permanent key that answers follow when the seed file changes: "{set}/{workType}/{subcategory or all}/{no:000}".
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>The flow the question belongs to (ProcessFlow.Code): "new-software-project".</summary>
    public string WorkType { get; set; } = string.Empty;

    /// <summary>The request source it is asked for; null = every subcategory ("TÜM ALT KATEGORİLER").</summary>
    public RequestSource? Subcategory { get; set; }

    /// <summary>The number on the form.</summary>
    public int No { get; set; }

    /// <summary>The form's list order.</summary>
    public int Order { get; set; }

    /// <summary>"1-Temel" (request analysis), "1" / "2" (level: document / research level).</summary>
    public string? Section { get; set; }

    /// <summary>Question / risk / control group.</summary>
    public string? Group { get; set; }

    /// <summary>Risk topic (risk analysis only).</summary>
    public string? Topic { get; set; }

    public string Text { get; set; } = string.Empty;

    /// <summary>Null when the form has no "Zorunluluk" column (level, internal approval).</summary>
    public QuestionRequirement? Requirement { get; set; }

    /// <summary>"Beklenen Bilgi / Çıktı" (request analysis).</summary>
    public string? ExpectedInfo { get; set; }

    /// <summary>"Olası Risk Tanımı" (risk analysis).</summary>
    public string? RiskDescription { get; set; }

    /// <summary>"Ön Görülen Aksiyon" (risk analysis).</summary>
    public string? SuggestedAction { get; set; }

    public string? Reference { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Options of this question only (level); empty for sets with shared options.</summary>
    public List<QuestionOption> Options { get; set; } = [];
}
