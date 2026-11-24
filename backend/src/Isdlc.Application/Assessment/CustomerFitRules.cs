using Isdlc.Application.Assessment.Dtos;
using Isdlc.Application.QuestionBank.Dtos;
using Isdlc.Domain.Assessment;

namespace Isdlc.Application.Assessment;

/// <summary>
/// Müşteri Uygunluk Değerlendirme Anket Formu (006): 10 questions, 0–100 points, a rating from the form's bands.
/// Only external requests list its questions. Pure: no database. Rules: docs/assessment-api.md → "Müşteri uygunluğu".
/// </summary>
public static class CustomerFitRules
{
    public const string SetCode = "customer-fit";

    public static CustomerFitResultDto Calculate(IReadOnlyList<QuestionnaireItemDto> items, IReadOnlyList<ScoreBand> bands)
    {
        var (score, maxScore, answered, listed) = ScoreBands.Total(items);
        var rating = listed > 0 && answered == listed ? RatingOf(bands, score) : null;

        return new CustomerFitResultDto(score, maxScore, answered, listed, rating);
    }

    /// <summary>The rating of a complete survey's score; the dashboard uses it too, so both show the same.</summary>
    public static CustomerFitRating? RatingOf(IReadOnlyList<ScoreBand> bands, int score) =>
        Enum.Parse<CustomerFitRating>(ScoreBands.ResultOf(ScoreBands.Of(bands, null), score));
}
