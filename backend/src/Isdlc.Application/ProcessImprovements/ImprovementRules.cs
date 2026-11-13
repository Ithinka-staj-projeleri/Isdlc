using Isdlc.Application.Common.Exceptions;
using Isdlc.Domain.ProcessImprovements;

namespace Isdlc.Application.ProcessImprovements;

/// <summary>
/// Prioritization of a proposal (docs/process-improvement-api.md → Önceliklendirme). Pure: every case unit tested.
/// The formula waits for the Kayıt Listesi (040); if it differs, only this class changes.
/// </summary>
public static class ImprovementRules
{
    public const string CodePrefix = "SI";
    public const string ScoresTogether = "Etki, uygulama sıklığı ve kolaylık birlikte girilmelidir.";

    public const int HighFrom = 12;
    public const int MediumFrom = 8;

    /// <summary>400 when only some of the three scores are given.</summary>
    public static void CheckScores(int? impact, int? frequency, int? ease)
    {
        var given = new[] { impact, frequency, ease }.Count(score => score is not null);
        if (given is not (0 or 3))
        {
            throw new BusinessRuleException(ScoresTogether);
        }
    }

    /// <summary>impact + frequency + ease (3–15); null until all three are given.</summary>
    public static int? ScoreOf(int? impact, int? frequency, int? ease) =>
        impact is { } i && frequency is { } f && ease is { } e ? i + f + e : null;

    /// <summary>12–15 High, 8–11 Medium, 3–7 Low; null without a score.</summary>
    public static ImprovementPriority? PriorityOf(int? score) => score switch
    {
        null => null,
        >= HighFrom => ImprovementPriority.High,
        >= MediumFrom => ImprovementPriority.Medium,
        _ => ImprovementPriority.Low,
    };
}
