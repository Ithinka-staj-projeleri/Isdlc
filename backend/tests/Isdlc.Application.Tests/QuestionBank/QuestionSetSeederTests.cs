using Isdlc.Domain.Projects;
using Isdlc.Domain.QuestionBank;
using Isdlc.Infrastructure.Persistence;
using Isdlc.Infrastructure.QuestionBank;
using Isdlc.Application.Tests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Isdlc.Application.Tests.QuestionBank;

/// <summary>
/// The question bank seed on a real database: the fixture starts the app's start-up seeding, so the tables are loaded
/// from the embedded files. Read-only checks; nothing here changes the shared data. Expectations: docs/question-bank-api.md → Seed.
/// </summary>
[Collection("Database")]
public class QuestionSetSeederTests(DatabaseFixture database)
{
    [Fact]
    public async Task Seed_AtStartUp_LoadsEveryQuestionOfEverySet()
    {
        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IsdlcDbContext>();

        var counts = await db.Questions.GroupBy(q => q.QuestionSet.Code)
            .Select(g => new { Code = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Code, x => x.Count);

        Assert.Equal(new Dictionary<string, int>
        {
            ["internal-approval"] = 43,
            ["customer-fit"] = 10,
            ["level"] = 74,
            ["request-analysis"] = 97,
            ["risk-analysis"] = 62,
        }, counts);
        Assert.True(await db.Questions.AllAsync(q => q.IsActive));
    }

    [Fact]
    public async Task Seed_LevelQuestions_HaveTheirOwnFiveOptions_SharedSetsHaveNone()
    {
        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IsdlcDbContext>();

        var levelOptionCounts = await db.Questions.Where(q => q.QuestionSet.Code == "level")
            .Select(q => q.Options.Count).Distinct().ToListAsync();
        var sharedSetQuestionOptions = await db.QuestionOptions
            .CountAsync(o => o.QuestionId != null && o.QuestionSet.AnswerMode == AnswerMode.SharedOptions);

        Assert.Equal([5], levelOptionCounts);
        Assert.Equal(0, sharedSetQuestionOptions);
    }

    [Fact]
    public async Task Seed_SharedOptions_KeepTheFormsLabelsAndMeanings()
    {
        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IsdlcDbContext>();

        var risk = await db.QuestionOptions.Where(o => o.QuestionSet.Code == "risk-analysis" && o.QuestionId == null)
            .OrderBy(o => o.Order)
            .Select(o => new { o.Label, o.Meaning, o.IsScored })
            .ToListAsync();

        Assert.Equal([
            new { Label = "Risk Var", Meaning = (OptionMeaning?)OptionMeaning.Negative, IsScored = true },
            new { Label = "Belirsiz (Bilgi Eksik)", Meaning = (OptionMeaning?)OptionMeaning.Open, IsScored = true },
            new { Label = "Risk Yok", Meaning = (OptionMeaning?)OptionMeaning.Complete, IsScored = false },
            new { Label = "Uygulanabilir Değil", Meaning = (OptionMeaning?)OptionMeaning.NotApplicable, IsScored = false },
        ], risk);
    }

    [Fact]
    public async Task Seed_AffiliatedQuestions_AreStoredWithTheirSubcategory()
    {
        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IsdlcDbContext>();

        var affiliated = await db.Questions.CountAsync(q => q.QuestionSet.Code == "risk-analysis" && q.Subcategory == RequestSource.Affiliated);

        Assert.Equal(8, affiliated);
    }

    [Fact]
    public async Task Seed_RunAgainWithTheSameFiles_WritesNothing()
    {
        // Every start runs the seeder; an unchanged file must leave the database untouched.
        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IsdlcDbContext>();
        var seeder = scope.ServiceProvider.GetRequiredService<QuestionSetSeeder>();
        var before = await db.Questions.CountAsync();

        await seeder.SeedAsync();

        Assert.False(db.ChangeTracker.HasChanges());
        Assert.Equal(before, await db.Questions.CountAsync());
    }
}
