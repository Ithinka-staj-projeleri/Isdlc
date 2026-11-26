using Isdlc.Application.Process.Dtos;
using Isdlc.Domain.Process;

namespace Isdlc.Application.Process;

/// <summary>
/// What the project has, as far as step prerequisites ask (Faz 7 · A4). Collected by ProcessService only for the
/// prerequisites of the step at hand. Names are the company's own: form names by set code, document names by type code.
/// </summary>
public record PrerequisiteFacts(
    bool RequestFormComplete,
    IReadOnlySet<string> CompletedQuestionnaires,
    bool LevelApplied,
    int MeetingCount,
    int RiskCount,
    int RequirementCount,
    int UnapprovedRequirementCount,
    IReadOnlySet<string> ApprovedDocumentCodes,
    IReadOnlyDictionary<string, string> QuestionnaireNames,
    IReadOnlyDictionary<string, string> DocumentNames);

/// <summary>
/// Whether a step's prerequisites hold. Pure, no database: the buttons on the process tab and the check in the commands
/// both use it, so they cannot disagree. Rules and messages: docs/configurability.md → Adım ön koşulları.
/// </summary>
public static class StepPrerequisites
{
    private const string AssessmentTab = "assessment";

    /// <summary>The prerequisites that do not hold, in the step's order; empty when the step may go on.</summary>
    public static IReadOnlyList<UnmetPrerequisiteDto> Evaluate(
        IEnumerable<StepPrerequisite> prerequisites, PrerequisiteFacts facts, int projectId) =>
        prerequisites
            .Where(p => !Holds(p, facts))
            .Select(p => Describe(p, facts, projectId))
            .ToList();

    public static bool Holds(StepPrerequisite prerequisite, PrerequisiteFacts facts) => prerequisite.Kind switch
    {
        PrerequisiteKind.RequestFormComplete => facts.RequestFormComplete,
        PrerequisiteKind.QuestionnaireComplete =>
            prerequisite.Value is { } set && facts.CompletedQuestionnaires.Contains(set),
        PrerequisiteKind.LevelApplied => facts.LevelApplied,
        PrerequisiteKind.MeetingExists => facts.MeetingCount > 0,
        PrerequisiteKind.RiskExists => facts.RiskCount > 0,
        PrerequisiteKind.RequirementsApproved => facts.RequirementCount > 0 && facts.UnapprovedRequirementCount == 0,
        PrerequisiteKind.DocumentApproved =>
            prerequisite.Value is { } type && facts.ApprovedDocumentCodes.Contains(type),
        _ => false,
    };

    /// <summary>The message the person sees and where the button takes them.</summary>
    public static UnmetPrerequisiteDto Describe(StepPrerequisite prerequisite, PrerequisiteFacts facts, int projectId)
    {
        var tab = $"/projects/{projectId}?tab=";
        return prerequisite.Kind switch
        {
            PrerequisiteKind.RequestFormComplete => new(prerequisite.Kind,
                "Talep Kayıt Formu'nun zorunlu alanları doldurulmalı.", "Talep formuna git", $"/projects/{projectId}/edit"),
            PrerequisiteKind.QuestionnaireComplete => new(prerequisite.Kind,
                $"{NameOf(facts.QuestionnaireNames, prerequisite.Value, "Form")} tamamlanmalı.",
                prerequisite.Value == "request-analysis" ? "Talep Analizi'ne git" : "Seviye & Uygunluk'a git",
                tab + (prerequisite.Value == "request-analysis" ? "request-analysis" : AssessmentTab)),
            PrerequisiteKind.LevelApplied => new(prerequisite.Kind,
                "Seviye projeye uygulanmalı.", "Seviye & Uygunluk'a git", tab + AssessmentTab),
            PrerequisiteKind.MeetingExists => new(prerequisite.Kind,
                "En az bir toplantı tutanağı girilmeli.", "Tutanaklara git", tab + "meetings"),
            PrerequisiteKind.RiskExists => new(prerequisite.Kind,
                "En az bir risk girilmeli.", "Risklere git", tab + "risks"),
            PrerequisiteKind.RequirementsApproved => new(prerequisite.Kind,
                "Gereksinimler onaylanmalı; taslak ya da incelemede gereksinim kalmamalı.", "Gereksinimlere git",
                tab + "requirements"),
            PrerequisiteKind.DocumentApproved => new(prerequisite.Kind,
                $"«{NameOf(facts.DocumentNames, prerequisite.Value, "Doküman")}» onaylanmalı.", "Dokümanlara git",
                tab + "documents"),
            _ => new(prerequisite.Kind, "Adımın ön koşulu sağlanmadı.", "Projeye git", $"/projects/{projectId}"),
        };
    }

    /// <summary>The 400 message when a command meets an unmet prerequisite: the first one, like the screen lists it.</summary>
    public static string BlockedMessage(IReadOnlyList<UnmetPrerequisiteDto> unmet) =>
        $"Bu adım tamamlanamaz: {unmet[0].Message}";

    private static string NameOf(IReadOnlyDictionary<string, string> names, string? code, string fallback) =>
        code is not null && names.TryGetValue(code, out var name) ? name : code ?? fallback;
}
