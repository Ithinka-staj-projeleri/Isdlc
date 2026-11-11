using System.Linq.Expressions;
using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.QuestionBank.Dtos;
using Isdlc.Domain.Projects;
using Isdlc.Domain.QuestionBank;

namespace Isdlc.Application.QuestionBank;

/// <summary>
/// The shared rules of every form (docs/question-bank-api.md → Ortak kurallar): which questions are listed,
/// which answers are valid, and the summary. Pure: no database, every case unit tested.
/// </summary>
public static class QuestionnaireRules
{
    /// <summary>
    /// The only work type the forms have so far (ProcessFlow.Code). Projects get their flow with Faz 4 · E1;
    /// until then every form lists the new software project questions (also for add-on work, decision 4).
    /// </summary>
    public const string DefaultWorkType = "new-software-project";

    /// <summary>Active questions of the work type, for the subcategory or for every subcategory (null).</summary>
    public static Expression<Func<Question, bool>> ListedIn(string workType, RequestSource subcategory) =>
        q => q.IsActive && q.WorkType == workType && (q.Subcategory == null || q.Subcategory == subcategory);

    /// <summary>A question without a requirement (level, internal approval) has to be answered like a required one.</summary>
    public static bool MustBeAnswered(QuestionDto question) => question.Requirement != QuestionRequirement.Conditional;

    /// <summary>Nothing to keep: the answer is emptied.</summary>
    public static bool IsEmpty(SaveAnswerDto answer) =>
        answer.OptionId is null
        && string.IsNullOrWhiteSpace(answer.Text)
        && string.IsNullOrWhiteSpace(answer.Note)
        && answer.Probability is null
        && answer.Severity is null;

    /// <summary>400 when the option is not the question's, or probability / severity come with an unscored answer.</summary>
    public static void Validate(QuestionDto question, SaveAnswerDto answer)
    {
        QuestionOptionDto? option = null;
        if (answer.OptionId is { } optionId)
        {
            option = question.Options.FirstOrDefault(o => o.Id == optionId)
                ?? throw new BusinessRuleException("Seçilen cevap bu soruya ait değil.");
        }

        if ((answer.Probability is not null || answer.Severity is not null) && option is not { IsScored: true })
        {
            throw new BusinessRuleException("Olasılık ve şiddet yalnızca puanlanan cevaplarda girilir.");
        }
    }

    /// <summary>The counts every form shows: answered, required left, meanings, notes the chosen option asks for.</summary>
    public static QuestionnaireSummaryDto Summarize(IReadOnlyList<QuestionnaireItemDto> items)
    {
        var chosen = items
            .Select(item => (item.Question, item.Answer, Option: OptionOf(item)))
            .ToList();

        var answered = chosen.Where(x => x.Option is not null).ToList();
        var requiredUnanswered = chosen.Count(x => x.Option is null && MustBeAnswered(x.Question));

        int Count(OptionMeaning meaning) => answered.Count(x => x.Option!.Meaning == meaning);

        return new QuestionnaireSummaryDto(
            Listed: items.Count,
            Answered: answered.Count,
            RequiredUnanswered: requiredUnanswered,
            IsComplete: requiredUnanswered == 0,
            ByMeaning: new OptionMeaningCountsDto(
                Count(OptionMeaning.Complete),
                Count(OptionMeaning.Partial),
                Count(OptionMeaning.Open),
                Count(OptionMeaning.Negative),
                Count(OptionMeaning.NotApplicable)),
            MissingNote: answered.Count(x => x.Option!.RequiresNote && string.IsNullOrWhiteSpace(x.Answer!.Note)));
    }

    /// <summary>The chosen option of a listed question; null when not chosen.</summary>
    private static QuestionOptionDto? OptionOf(QuestionnaireItemDto item) =>
        item.Answer?.OptionId is { } id ? item.Question.Options.FirstOrDefault(o => o.Id == id) : null;

    /// <summary>Whitespace-only text is not kept.</summary>
    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
