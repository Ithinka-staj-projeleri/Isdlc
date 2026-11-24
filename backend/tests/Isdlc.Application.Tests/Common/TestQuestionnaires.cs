using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.QuestionBank;
using Isdlc.Domain.Projects;
using Isdlc.Domain.QuestionBank;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Isdlc.Application.Tests.Common;

/// <summary>
/// Fills a project's form straight in the database, for tests that need answered forms (results, dashboard).
/// The questions are the seeded ones of the new software project flow.
/// </summary>
public static class TestQuestionnaires
{
    /// <summary>
    /// Answers every listed question of the set (optionally only those of one section) with the option choose picks;
    /// a null pick leaves the question unanswered. Returns how many questions were answered.
    /// </summary>
    public static async Task<int> AnswerAsync(
        DatabaseFixture database, int projectId, string setCode, RequestSource subcategory,
        Func<Question, QuestionOption?> choose, string? section = null)
    {
        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var set = await db.QuestionSets.SingleAsync(s => s.Code == setCode);
        var questions = await db.Questions
            .Include(q => q.Options)
            .Where(q => q.QuestionSetId == set.Id)
            .Where(QuestionnaireRules.ListedIn(QuestionnaireRules.DefaultWorkType, subcategory))
            .Where(q => section == null || q.Section == section)
            .OrderBy(q => q.Order)
            .ToListAsync();

        var form = await db.Questionnaires.SingleOrDefaultAsync(x => x.ProjectId == projectId && x.QuestionSetId == set.Id);
        if (form is null)
        {
            form = new Questionnaire
            {
                ProjectId = projectId, QuestionSetId = set.Id,
                WorkType = QuestionnaireRules.DefaultWorkType, Subcategory = subcategory,
            };
            db.Questionnaires.Add(form);
        }

        var answered = 0;
        foreach (var question in questions)
        {
            if (choose(question) is not { } option)
            {
                continue;
            }

            db.QuestionAnswers.Add(new QuestionAnswer
            {
                Questionnaire = form, ProjectId = projectId, QuestionId = question.Id, OptionId = option.Id,
            });
            answered++;
        }

        await db.SaveChangesAsync();
        return answered;
    }

    /// <summary>The option worth the given points.</summary>
    public static QuestionOption Points(Question question, int points) => question.Options.Single(o => o.Points == points);

    public static QuestionOption Highest(Question question) => question.Options.MaxBy(o => o.Points ?? 0)!;

    public static QuestionOption Lowest(Question question) => question.Options.MinBy(o => o.Points ?? 0)!;
}
