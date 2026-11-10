using Isdlc.Application.Assessment.Dtos;
using Isdlc.Domain.Assessment;

namespace Isdlc.Application.Assessment;

/// <summary>
/// The level questions, acceptance criteria and their calculations: the only place they are written
/// (the screen reads them from GET /assessment/definition). No database access, so they can be unit tested directly.
/// Source: the prototype's form; when the company's level template arrives, only this file changes.
/// Rules: docs/assessment-api.md → "Sorular ve ölçütler".
/// </summary>
public static class AssessmentRules
{
    /// <summary>Effort above 80 person-days (answer 3) makes a project level 3 on its own.</summary>
    public const int HighEffort = 3;
    public const int Level3MinScore = 14;
    public const int Level2MinScore = 9;

    public const decimal AcceptMinScore = 3.50m;
    public const decimal ConditionalMinScore = 2.50m;

    public static readonly IReadOnlyList<LevelQuestionDto> LevelQuestions =
    [
        Question("effort", "Tahmini efor", "≤ 20 adam-gün", "21–80 adam-gün", "> 80 adam-gün"),
        Question("screensAndModules", "Modül / ekran sayısı",
            "1–5 ekran, tek modül", "6–20 ekran veya 2–3 modül", "> 20 ekran veya 4+ modül"),
        Question("integrations", "Dış sistem entegrasyonu", "Yok", "1–2 servis", "3+ servis / çift yönlü"),
        Question("userRoles", "Kullanıcı rolü ve kitlesi",
            "Tek rol, iç kullanıcı", "2–4 rol", "5+ rol veya son kullanıcı / vatandaş"),
        Question("dataSensitivity", "Veri hassasiyeti (KVKK)",
            "Kişisel veri yok", "Kişisel veri var", "Özel nitelikli kişisel veri / finansal veri"),
        Question("hardwareDependency", "Donanım / cihaz bağımlılığı",
            "Yok", "Standart cihaz (yazıcı, barkod)", "Özel cihaz / IoT"),
    ];

    /// <summary>Weights add up to 100.</summary>
    public static readonly IReadOnlyList<AcceptanceCriterionDto> AcceptanceCriteria =
    [
        new("strategicFit", "Stratejik uyum (şirket hedefleri, ürün yol haritası)", 20),
        new("commercialValue", "Ticari değer / gelir potansiyeli", 20),
        new("technicalFeasibility", "Teknik uygulanabilirlik ve mevcut yetkinlik", 20),
        new("resourceAndSchedule", "Kaynak ve takvim uygunluğu", 15),
        new("customerFit", "Müşteri uygunluğu (ödeme, iletişim, geçmiş)", 15),
        new("riskLevel", "Risk düzeyi (yüksek puan = düşük risk)", 10),
    ];

    public static AssessmentDefinitionDto Definition { get; } = new(
        LevelQuestions,
        "Efor 80 adam-günü aşıyorsa veya puan 14 ve üstüyse Seviye 3; puan 9–13 Seviye 2; daha azı Seviye 1.",
        AcceptanceCriteria,
        "Ağırlıklı ortalama 3,50 ve üstü kabul; 2,50–3,49 koşullu kabul; 2,50'nin altı ret / erteleme önerisi.");

    // ---- Level ----

    /// <summary>The answers in the order of LevelQuestions.</summary>
    public static int?[] LevelValues(LevelAnswersDto a) =>
        [a.Effort, a.ScreensAndModules, a.Integrations, a.UserRoles, a.DataSensitivity, a.HardwareDependency];

    public static int AnsweredCount(LevelAnswersDto answers) => LevelValues(answers).Count(v => v is not null);

    /// <summary>Sum of the answers, 6–18; null until every question is answered.</summary>
    public static int? LevelScore(LevelAnswersDto answers)
    {
        var values = LevelValues(answers);
        return values.All(v => v is not null) ? values.Sum(v => v!.Value) : null;
    }

    /// <summary>Effort 3 or score ≥ 14 → 3; score 9–13 → 2; less → 1. Null until every question is answered.</summary>
    public static int? SuggestedLevel(LevelAnswersDto answers)
    {
        if (LevelScore(answers) is not { } score)
        {
            return null;
        }

        if (answers.Effort == HighEffort || score >= Level3MinScore)
        {
            return 3;
        }

        return score >= Level2MinScore ? 2 : 1;
    }

    // ---- Acceptance ----

    /// <summary>The scores in the order of AcceptanceCriteria.</summary>
    public static int?[] AcceptanceValues(AcceptanceScoresDto s) =>
        [s.StrategicFit, s.CommercialValue, s.TechnicalFeasibility, s.ResourceAndSchedule, s.CustomerFit, s.RiskLevel];

    public static int AnsweredCount(AcceptanceScoresDto scores) => AcceptanceValues(scores).Count(v => v is not null);

    /// <summary>Σ (score × weight) / 100 with two decimals, 1.00–5.00; null until every criterion is scored.</summary>
    public static decimal? WeightedScore(AcceptanceScoresDto scores)
    {
        var values = AcceptanceValues(scores);
        if (values.Any(v => v is null))
        {
            return null;
        }

        var total = values.Select((v, i) => v!.Value * AcceptanceCriteria[i].Weight).Sum();
        return Math.Round(total / 100m, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>≥ 3.50 Accept; 2.50–3.49 Conditional; &lt; 2.50 RejectOrPostpone.</summary>
    public static AcceptanceRecommendation RecommendationOf(decimal weightedScore) =>
        weightedScore >= AcceptMinScore ? AcceptanceRecommendation.Accept
        : weightedScore >= ConditionalMinScore ? AcceptanceRecommendation.Conditional
        : AcceptanceRecommendation.RejectOrPostpone;

    // ---- Helpers ----

    private static LevelQuestionDto Question(string key, string title, string option1, string option2, string option3) =>
        new(key, title, [new(1, option1), new(2, option2), new(3, option3)]);
}
