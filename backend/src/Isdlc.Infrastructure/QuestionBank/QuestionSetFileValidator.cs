using Isdlc.Domain.QuestionBank;

namespace Isdlc.Infrastructure.QuestionBank;

/// <summary>
/// Checks the question set files before anything is written. Any problem stops the start-up, so a wrong file
/// never reaches the database half-loaded. Pure: no database, unit tested directly.
/// </summary>
public static class QuestionSetFileValidator
{
    // Same limits as the columns (QuestionBankConfigurations).
    private const int TextMax = 2000;
    private const int ReferenceMax = 1000;
    private const int GroupMax = 200;
    private const int SectionMax = 50;
    private const int LabelMax = 500;
    private const int WorkTypeMax = 50;

    /// <summary>Every problem found, in English for the log; empty when the files are fine.</summary>
    public static IReadOnlyList<string> Validate(IReadOnlyList<QuestionSetFile> files)
    {
        var errors = new List<string>();

        foreach (var code in files.GroupBy(f => f.Code).Where(g => g.Count() > 1).Select(g => g.Key))
        {
            errors.Add($"Question set '{code}' appears in more than one file.");
        }

        foreach (var file in files)
        {
            ValidateFile(file, errors);
        }

        return errors;
    }

    private static void ValidateFile(QuestionSetFile file, List<string> errors)
    {
        var set = file.Code;
        if (string.IsNullOrWhiteSpace(set))
        {
            errors.Add("A question set file has no code.");
            return;
        }

        if (file.AnswerMode == AnswerMode.SharedOptions && file.Options.Count == 0)
        {
            errors.Add($"{set}: shared options are missing.");
        }

        if (file.AnswerMode == AnswerMode.QuestionOptions && file.Options.Count > 0)
        {
            errors.Add($"{set}: a set whose questions have their own options cannot also have shared options.");
        }

        foreach (var code in file.Options.GroupBy(o => o.Code).Where(g => g.Count() > 1).Select(g => g.Key))
        {
            errors.Add($"{set}: option code '{code}' repeats.");
        }

        foreach (var option in file.Options.Where(o => string.IsNullOrWhiteSpace(o.Label) || o.Label.Length > LabelMax))
        {
            errors.Add($"{set}: option '{option.Code}' has an empty or too long label.");
        }

        foreach (var key in file.Questions.GroupBy(file.QuestionKey).Where(g => g.Count() > 1).Select(g => g.Key))
        {
            errors.Add($"{set}: question key '{key}' repeats.");
        }

        foreach (var question in file.Questions)
        {
            ValidateQuestion(file, question, errors);
        }

        ValidateBands(file, errors);
    }

    private static void ValidateQuestion(QuestionSetFile file, QuestionSeed q, List<string> errors)
    {
        var where = $"{file.Code} no {q.No}";

        if (string.IsNullOrWhiteSpace(q.WorkType) || q.WorkType.Length > WorkTypeMax)
        {
            errors.Add($"{where}: work type is empty or too long.");
        }

        if (string.IsNullOrWhiteSpace(q.Text))
        {
            errors.Add($"{where}: text is empty.");
        }

        TooLong(where, "text", q.Text, TextMax, errors);
        TooLong(where, "expectedInfo", q.ExpectedInfo, TextMax, errors);
        TooLong(where, "riskDescription", q.RiskDescription, TextMax, errors);
        TooLong(where, "suggestedAction", q.SuggestedAction, TextMax, errors);
        TooLong(where, "reference", q.Reference, ReferenceMax, errors);
        TooLong(where, "group", q.Group, GroupMax, errors);
        TooLong(where, "topic", q.Topic, GroupMax, errors);
        TooLong(where, "section", q.Section, SectionMax, errors);

        var options = q.Options ?? [];
        if (file.AnswerMode == AnswerMode.QuestionOptions)
        {
            if (options.Count == 0)
            {
                errors.Add($"{where}: options are missing.");
            }

            foreach (var option in options.Where(o => string.IsNullOrWhiteSpace(o.Label) || o.Label.Length > LabelMax))
            {
                errors.Add($"{where}: an option has an empty or too long label.");
            }

            if (options.Any(o => o.Points < 0))
            {
                errors.Add($"{where}: an option has negative points.");
            }
        }
        else if (options.Count > 0)
        {
            errors.Add($"{where}: the set uses shared options, so a question cannot have its own.");
        }
    }

    /// <summary>Within a section the ranges must not overlap or leave gaps, and must go up.</summary>
    private static void ValidateBands(QuestionSetFile file, List<string> errors)
    {
        foreach (var band in file.Bands ?? [])
        {
            var where = $"{file.Code} bands '{band.Title}'";
            if (band.Ranges.Count == 0)
            {
                errors.Add($"{where}: no ranges.");
                continue;
            }

            for (var i = 0; i < band.Ranges.Count; i++)
            {
                var range = band.Ranges[i];
                if (range.Min > range.Max)
                {
                    errors.Add($"{where}: range {range.Min}–{range.Max} goes down.");
                }

                if (i > 0 && range.Min != band.Ranges[i - 1].Max + 1)
                {
                    errors.Add($"{where}: ranges {band.Ranges[i - 1].Max} and {range.Min} leave a gap or overlap.");
                }
            }
        }

        foreach (var section in (file.Bands ?? []).GroupBy(b => b.Section).Where(g => g.Count() > 1).Select(g => g.Key))
        {
            errors.Add($"{file.Code}: bands for section '{section}' repeat.");
        }
    }

    private static void TooLong(string where, string field, string? value, int max, List<string> errors)
    {
        if (value is not null && value.Length > max)
        {
            errors.Add($"{where}: {field} is longer than {max} characters.");
        }
    }
}
