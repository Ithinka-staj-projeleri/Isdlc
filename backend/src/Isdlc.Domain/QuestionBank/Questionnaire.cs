using Isdlc.Domain.Common;
using Isdlc.Domain.Identity;
using Isdlc.Domain.Projects;

namespace Isdlc.Domain.QuestionBank;

/// <summary>
/// A form filled in for a project: one per project and question set, created with the first answer.
/// Project data: audited, never deleted. Rules: docs/question-bank-api.md → Ortak kurallar.
/// </summary>
public class Questionnaire : AuditableEntity, IAudited, IProjectRecord
{
    public int Id { get; set; }

    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public int QuestionSetId { get; set; }
    public QuestionSet QuestionSet { get; set; } = null!;

    /// <summary>The flow whose questions are listed (ProcessFlow.Code).</summary>
    public string WorkType { get; set; } = string.Empty;

    /// <summary>Taken from the project's request source when the form is created; changing it changes the list.</summary>
    public RequestSource Subcategory { get; set; }

    /// <summary>Who took part ("Değerlendirmeye Katılanlar", "İnceleyenler").</summary>
    public string? Participants { get; set; }

    /// <summary>Meeting / assessment / review date.</summary>
    public DateOnly? AssessedOn { get; set; }

    /// <summary>Who approved the form (internal approval: "İç Onayı Veren").</summary>
    public int? ApprovedById { get; set; }
    public User? ApprovedBy { get; set; }

    public DateOnly? ApprovedOn { get; set; }

    public List<QuestionAnswer> Answers { get; set; } = [];
}
