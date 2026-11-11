using Isdlc.Domain.Common;
using Isdlc.Domain.Projects;

namespace Isdlc.Domain.QuestionBank;

/// <summary>
/// The answer to one question of a filled-in form. Project data: audited with its project, never deleted
/// (an answer is emptied instead). ProjectId repeats the questionnaire's so the audit log can list it per project.
/// </summary>
public class QuestionAnswer : AuditableEntity, IAudited, IProjectRecord
{
    public const int MinLevel = 1;
    public const int MaxLevel = 3;

    public int Id { get; set; }

    public int QuestionnaireId { get; set; }
    public Questionnaire Questionnaire { get; set; } = null!;

    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public int QuestionId { get; set; }
    public Question Question { get; set; } = null!;

    public int? OptionId { get; set; }
    public QuestionOption? Option { get; set; }

    /// <summary>The stakeholder's answer in their own words (request analysis).</summary>
    public string? Text { get; set; }

    /// <summary>Open issue / missing information, risk reason, or the explanation the form asks for.</summary>
    public string? Note { get; set; }

    /// <summary>1–3, only with a scored option (risk analysis).</summary>
    public int? Probability { get; set; }

    /// <summary>1–3, only with a scored option (risk analysis).</summary>
    public int? Severity { get; set; }
}
