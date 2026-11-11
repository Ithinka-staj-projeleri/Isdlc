using System.ComponentModel.DataAnnotations;
using Isdlc.Application.Common.Models;
using Isdlc.Domain.Projects;
using Isdlc.Domain.QuestionBank;

namespace Isdlc.Application.QuestionBank.Dtos;

// Contract: docs/question-bank-api.md → DTO'lar

/// <summary>One of the company's forms.</summary>
public record QuestionSetDto(string Code, string Name, string SourceDocument, string Version, AnswerMode AnswerMode);

/// <summary>
/// A choice for a question: the set's shared options (request analysis, risk analysis, internal approval)
/// or the question's own options with points (level). Code is the key without the set prefix ("risk-present").
/// </summary>
public record QuestionOptionDto(
    int Id, string Code, string Label, int? Points, OptionMeaning? Meaning, bool IsScored, bool RequiresNote);

/// <summary>A question with its options, in the order the form lists it. Code is the permanent seed key.</summary>
public record QuestionDto(
    int Id,
    string Code,
    int No,
    string? Section,
    string? Group,
    string? Topic,
    string Text,
    QuestionRequirement? Requirement,
    string? ExpectedInfo,
    string? RiskDescription,
    string? SuggestedAction,
    string? Reference,
    IReadOnlyList<QuestionOptionDto> Options);

/// <summary>?workType=new-software-project&amp;subcategory=External</summary>
public record QuestionFilter
{
    /// <summary>The flow's key; the only one the forms have so far when left empty.</summary>
    [MaxLength(50)]
    public string? WorkType { get; init; }

    [Required(ErrorMessage = "Alt kategori seçilmelidir.")]
    public RequestSource? Subcategory { get; init; }
}

/// <summary>A saved answer. A question without an answer row has a null answer.</summary>
public record AnswerDto(
    int? OptionId,
    string? Text,
    string? Note,
    int? Probability,
    int? Severity,
    UserSummaryDto? UpdatedBy,
    DateTime? UpdatedAt);

public record QuestionnaireItemDto(QuestionDto Question, AnswerDto? Answer);

/// <summary>How many answers have each meaning (level options have none, so level answers are not counted here).</summary>
public record OptionMeaningCountsDto(int Complete, int Partial, int Open, int Negative, int NotApplicable);

/// <summary>The shared summary of every form; the form's own result (level, ratio, risk degree) is in Result.</summary>
public record QuestionnaireSummaryDto(
    int Listed,
    int Answered,
    int RequiredUnanswered,
    bool IsComplete,
    OptionMeaningCountsDto ByMeaning,
    int MissingNote);

public record QuestionnaireDto(
    int ProjectId,
    QuestionSetDto Set,
    string WorkType,
    RequestSource Subcategory,
    string? Participants,
    DateOnly? AssessedOn,
    UserSummaryDto? ApprovedBy,
    DateOnly? ApprovedOn,
    IReadOnlyList<QuestionnaireItemDto> Questions,
    QuestionnaireSummaryDto Summary,
    object? Result);

/// <summary>The form's header (PUT .../questionnaires/{setCode}).</summary>
public record SaveQuestionnaireDto
{
    [Required(ErrorMessage = "Alt kategori seçilmelidir.")]
    public RequestSource? Subcategory { get; init; }

    [MaxLength(1000, ErrorMessage = "Katılanlar en fazla 1000 karakter olabilir.")]
    public string? Participants { get; init; }

    public DateOnly? AssessedOn { get; init; }

    public int? ApprovedByUserId { get; init; }

    public DateOnly? ApprovedOn { get; init; }
}

/// <summary>One question's answer (PUT .../answers/{questionId}). Every field empty = the answer is emptied.</summary>
public record SaveAnswerDto
{
    public int? OptionId { get; init; }

    [MaxLength(4000, ErrorMessage = "Yanıt en fazla 4000 karakter olabilir.")]
    public string? Text { get; init; }

    [MaxLength(2000, ErrorMessage = "Not en fazla 2000 karakter olabilir.")]
    public string? Note { get; init; }

    [Range(QuestionAnswer.MinLevel, QuestionAnswer.MaxLevel, ErrorMessage = "Olasılık 1 ile 3 arasında olmalıdır.")]
    public int? Probability { get; init; }

    [Range(QuestionAnswer.MinLevel, QuestionAnswer.MaxLevel, ErrorMessage = "Şiddet 1 ile 3 arasında olmalıdır.")]
    public int? Severity { get; init; }
}
