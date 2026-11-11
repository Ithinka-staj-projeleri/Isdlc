using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.QuestionBank.Dtos;
using Isdlc.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace Isdlc.Application.QuestionBank;

/// <summary>A set as the services use it: its id and what the screen gets.</summary>
internal record QuestionSetRef(int Id, QuestionSetDto Dto);

/// <summary>Read-only queries the question bank and the project forms share.</summary>
internal static class QuestionBankQueries
{
    /// <summary>404 when there is no such set.</summary>
    public static async Task<QuestionSetRef> GetSetAsync(
        this IApplicationDbContext db, string setCode, CancellationToken cancellationToken) =>
        await db.QuestionSets
            .AsNoTracking()
            .Where(s => s.Code == setCode)
            .Select(s => new QuestionSetRef(s.Id, new QuestionSetDto(s.Code, s.Name, s.SourceDocument, s.Version, s.AnswerMode)))
            .SingleOrDefaultAsync(cancellationToken)
        ?? throw new NotFoundException("Soru seti bulunamadı.");

    /// <summary>
    /// The questions a form lists, in the form's order, each with its choices: its own (level) or the set's shared ones.
    /// questionId narrows it to one question (empty list when that question is not listed).
    /// </summary>
    public static async Task<List<QuestionDto>> ListQuestionsAsync(
        this IApplicationDbContext db, QuestionSetRef set, string workType, RequestSource subcategory,
        CancellationToken cancellationToken, int? questionId = null)
    {
        var prefix = set.Dto.Code + "/";

        var sharedOptions = await db.QuestionOptions
            .AsNoTracking()
            .Where(o => o.QuestionSetId == set.Id && o.QuestionId == null)
            .OrderBy(o => o.Order)
            .Select(o => new QuestionOptionDto(o.Id, o.Key, o.Label, o.Points, o.Meaning, o.IsScored, o.RequiresNote))
            .ToListAsync(cancellationToken);

        var query = db.Questions
            .AsNoTracking()
            .Where(q => q.QuestionSetId == set.Id)
            .Where(QuestionnaireRules.ListedIn(workType, subcategory));

        if (questionId is { } id)
        {
            query = query.Where(q => q.Id == id);
        }

        var questions = await query
            .OrderBy(q => q.Order)
            .ThenBy(q => q.No)
            .Select(q => new
            {
                q.Id, q.Key, q.No, q.Section, q.Group, q.Topic, q.Text, q.Requirement,
                q.ExpectedInfo, q.RiskDescription, q.SuggestedAction, q.Reference,
                Options = q.Options
                    .OrderBy(o => o.Order)
                    .Select(o => new QuestionOptionDto(o.Id, o.Key, o.Label, o.Points, o.Meaning, o.IsScored, o.RequiresNote))
                    .ToList(),
            })
            .ToListAsync(cancellationToken);

        var shared = sharedOptions.Select(o => WithShortCode(o, prefix)).ToList();

        return questions
            .Select(q => new QuestionDto(
                q.Id, q.Key, q.No, q.Section, q.Group, q.Topic, q.Text, q.Requirement,
                q.ExpectedInfo, q.RiskDescription, q.SuggestedAction, q.Reference,
                q.Options.Count > 0 ? q.Options.Select(o => WithShortCode(o, prefix)).ToList() : shared))
            .ToList();
    }

    /// <summary>"risk-analysis/risk-present" → "risk-present".</summary>
    private static QuestionOptionDto WithShortCode(QuestionOptionDto option, string prefix) =>
        option.Code.StartsWith(prefix, StringComparison.Ordinal) ? option with { Code = option.Code[prefix.Length..] } : option;
}
