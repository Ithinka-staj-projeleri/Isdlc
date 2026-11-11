using Isdlc.Domain.QuestionBank;
using Isdlc.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Isdlc.Infrastructure.QuestionBank;

/// <summary>
/// Loads Seed/question-sets/*.json at start-up and keeps the database in line with them (docs/question-bank-api.md → Seed):
/// sets, questions and options are matched by their permanent keys; texts are updated in place, new ones are added,
/// a question that left the file becomes inactive (never deleted, so its answers stay). Runs on every start; an unchanged
/// database is not written to. Wrong files stop the start-up before anything is written.
/// </summary>
public class QuestionSetSeeder(IsdlcDbContext db, ILogger<QuestionSetSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var files = QuestionSetFile.ReadAll();
        var errors = QuestionSetFileValidator.Validate(files);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Question set files are not valid:" + Environment.NewLine + string.Join(Environment.NewLine, errors));
        }

        var sets = await db.QuestionSets
            .Include(s => s.Options)
            .Include(s => s.Questions)
            .ToDictionaryAsync(s => s.Code, cancellationToken);

        var changes = 0;
        foreach (var file in files)
        {
            if (!sets.TryGetValue(file.Code, out var set))
            {
                set = new QuestionSet { Code = file.Code };
                db.QuestionSets.Add(set);
                changes++;
            }

            changes += SyncSet(set, file);
        }

        if (changes > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Question bank: {Changes} values written from {Files} question set files.", changes, files.Count);
        }
    }

    private int SyncSet(QuestionSet set, QuestionSetFile file)
    {
        var changes = 0;
        changes += Set(set.Name, file.Name, v => set.Name = v);
        changes += Set(set.SourceDocument, file.SourceDocument, v => set.SourceDocument = v);
        changes += Set(set.Version, file.Version, v => set.Version = v);
        changes += Set(set.AnswerMode, file.AnswerMode, v => set.AnswerMode = v);
        changes += Set(set.Bands, file.BandsJson(), v => set.Bands = v);

        var optionsByKey = set.Options.ToDictionary(o => o.Key);
        for (var i = 0; i < file.Options.Count; i++)
        {
            var seed = file.Options[i];
            var key = file.SharedOptionKey(seed);
            if (!optionsByKey.TryGetValue(key, out var option))
            {
                option = new QuestionOption { Key = key };
                set.Options.Add(option);
                optionsByKey[key] = option;
                changes++;
            }

            changes += SyncOption(option, seed.Label, points: null, seed.Meaning, seed.IsScored, seed.RequiresNote, order: i + 1);
        }

        var questionsByKey = set.Questions.ToDictionary(q => q.Key);
        var keysInFile = new HashSet<string>();
        foreach (var seed in file.Questions)
        {
            var key = file.QuestionKey(seed);
            keysInFile.Add(key);
            if (!questionsByKey.TryGetValue(key, out var question))
            {
                question = new Question { Key = key };
                set.Questions.Add(question);
                changes++;
            }

            changes += SyncQuestion(question, seed);
            changes += SyncQuestionOptions(set, question, seed, optionsByKey);
        }

        // Out of the file: kept for its answers and history, but no longer listed on new or open forms.
        foreach (var question in set.Questions.Where(q => q.IsActive && q.Key.Length > 0 && !keysInFile.Contains(q.Key)))
        {
            question.IsActive = false;
            changes++;
            logger.LogInformation("Question bank: question {Key} left the seed file and is now inactive.", question.Key);
        }

        return changes;
    }

    private static int SyncQuestion(Question question, QuestionSeed seed)
    {
        var changes = 0;
        changes += Set(question.WorkType, seed.WorkType, v => question.WorkType = v);
        changes += Set(question.Subcategory, seed.Subcategory, v => question.Subcategory = v);
        changes += Set(question.No, seed.No, v => question.No = v);
        changes += Set(question.Order, seed.Order, v => question.Order = v);
        changes += Set(question.Section, seed.Section, v => question.Section = v);
        changes += Set(question.Group, seed.Group, v => question.Group = v);
        changes += Set(question.Topic, seed.Topic, v => question.Topic = v);
        changes += Set(question.Text, seed.Text, v => question.Text = v);
        changes += Set(question.Requirement, seed.Requirement, v => question.Requirement = v);
        changes += Set(question.ExpectedInfo, seed.ExpectedInfo, v => question.ExpectedInfo = v);
        changes += Set(question.RiskDescription, seed.RiskDescription, v => question.RiskDescription = v);
        changes += Set(question.SuggestedAction, seed.SuggestedAction, v => question.SuggestedAction = v);
        changes += Set(question.Reference, seed.Reference, v => question.Reference = v);
        changes += Set(question.IsActive, true, v => question.IsActive = v);
        return changes;
    }

    /// <summary>A question's own options (level), matched by position: option 1 stays option 1 when its label is corrected.</summary>
    private static int SyncQuestionOptions(
        QuestionSet set, Question question, QuestionSeed seed, Dictionary<string, QuestionOption> optionsByKey)
    {
        var changes = 0;
        var seeds = seed.Options ?? [];
        for (var i = 0; i < seeds.Count; i++)
        {
            var key = QuestionSetFile.QuestionOptionKey(question.Key, i + 1);
            if (!optionsByKey.TryGetValue(key, out var option))
            {
                option = new QuestionOption { Key = key, Question = question };
                set.Options.Add(option);
                optionsByKey[key] = option;
                changes++;
            }

            changes += SyncOption(option, seeds[i].Label, seeds[i].Points, meaning: null, isScored: false, requiresNote: false, order: i + 1);
        }

        return changes;
    }

    private static int SyncOption(
        QuestionOption option, string label, int? points, OptionMeaning? meaning, bool isScored, bool requiresNote, int order)
    {
        var changes = 0;
        changes += Set(option.Label, label, v => option.Label = v);
        changes += Set(option.Points, points, v => option.Points = v);
        changes += Set(option.Meaning, meaning, v => option.Meaning = v);
        changes += Set(option.IsScored, isScored, v => option.IsScored = v);
        changes += Set(option.RequiresNote, requiresNote, v => option.RequiresNote = v);
        changes += Set(option.Order, order, v => option.Order = v);
        return changes;
    }

    /// <summary>Sets a value only when it differs, so an unchanged database is not written to. Returns 1 if changed.</summary>
    private static int Set<T>(T current, T wanted, Action<T> set)
    {
        if (EqualityComparer<T>.Default.Equals(current, wanted))
        {
            return 0;
        }

        set(wanted);
        return 1;
    }
}
