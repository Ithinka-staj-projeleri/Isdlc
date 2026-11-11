using System.Text.Json;
using System.Text.Json.Serialization;
using Isdlc.Domain.Projects;
using Isdlc.Domain.QuestionBank;

namespace Isdlc.Infrastructure.QuestionBank;

/// <summary>
/// Shape of Seed/question-sets/{code}.json, and how it is read, checked and keyed.
/// The files are built once from the company's Excel forms; the templates themselves are not in the repo.
/// Rules: docs/question-bank-api.md → Seed.
/// </summary>
public record QuestionSetFile(
    string Code,
    string Name,
    string SourceDocument,
    string Version,
    AnswerMode AnswerMode,
    List<SharedOptionSeed> Options,
    List<BandSeed>? Bands,
    List<QuestionSeed> Questions)
{
    private const string ResourcePrefix = "Isdlc.Infrastructure.Seed.question_sets.";

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>All question set files embedded in the assembly, in file name order.</summary>
    public static IReadOnlyList<QuestionSetFile> ReadAll()
    {
        var assembly = typeof(QuestionSetFile).Assembly;
        return assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal) && n.EndsWith(".json", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(name =>
            {
                using var stream = assembly.GetManifestResourceStream(name)!;
                return JsonSerializer.Deserialize<QuestionSetFile>(stream, JsonOptions)
                    ?? throw new InvalidOperationException($"Question set file '{name}' is empty.");
            })
            .ToList();
    }

    /// <summary>The bands as stored on QuestionSet.Bands (null when the form has none).</summary>
    public string? BandsJson() => Bands is null ? null : JsonSerializer.Serialize(Bands, JsonOptions);

    /// <summary>"risk-analysis/new-software-project/all/001": the key answers follow when the file changes.</summary>
    public string QuestionKey(QuestionSeed q) =>
        $"{Code}/{q.WorkType}/{(q.Subcategory is { } s ? s.ToString().ToLowerInvariant() : "all")}/{q.No:000}";

    public string SharedOptionKey(SharedOptionSeed o) => $"{Code}/{o.Code}";

    public static string QuestionOptionKey(string questionKey, int position) => $"{questionKey}/{position}";
}

/// <summary>An option shared by every question of the set ("Yanıtlandı", "Risk Var").</summary>
public record SharedOptionSeed(string Code, string Label, OptionMeaning Meaning, bool IsScored, bool RequiresNote);

/// <summary>One question; Options only for sets whose questions have their own options (level).</summary>
public record QuestionSeed(
    string WorkType,
    RequestSource? Subcategory,
    int No,
    int Order,
    string Text,
    string? Section = null,
    string? Group = null,
    string? Topic = null,
    QuestionRequirement? Requirement = null,
    string? ExpectedInfo = null,
    string? RiskDescription = null,
    string? SuggestedAction = null,
    string? Reference = null,
    List<QuestionOptionSeed>? Options = null);

public record QuestionOptionSeed(string Label, int Points);

/// <summary>The form's own thresholds for one section (null section = the whole form).</summary>
public record BandSeed(string? Section, string Title, List<BandRangeSeed> Ranges);

public record BandRangeSeed(int Min, int Max, string Result, string? Priority = null);
