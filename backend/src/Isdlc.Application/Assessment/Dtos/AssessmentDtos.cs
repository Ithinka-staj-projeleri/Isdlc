using System.ComponentModel.DataAnnotations;
using Isdlc.Application.Common.Models;
using Isdlc.Domain.Assessment;

namespace Isdlc.Application.Assessment.Dtos;

/// <summary>Answers of the level questions (flow 2.4). Each 1–3; null means not answered yet.</summary>
public record LevelAnswersDto
{
    [Range(1, 3, ErrorMessage = "Cevap 1 ile 3 arasında olmalıdır.")]
    public int? Effort { get; init; }

    [Range(1, 3, ErrorMessage = "Cevap 1 ile 3 arasında olmalıdır.")]
    public int? ScreensAndModules { get; init; }

    [Range(1, 3, ErrorMessage = "Cevap 1 ile 3 arasında olmalıdır.")]
    public int? Integrations { get; init; }

    [Range(1, 3, ErrorMessage = "Cevap 1 ile 3 arasında olmalıdır.")]
    public int? UserRoles { get; init; }

    [Range(1, 3, ErrorMessage = "Cevap 1 ile 3 arasında olmalıdır.")]
    public int? DataSensitivity { get; init; }

    [Range(1, 3, ErrorMessage = "Cevap 1 ile 3 arasında olmalıdır.")]
    public int? HardwareDependency { get; init; }
}

/// <summary>Scores of the acceptance criteria (flow 3.1). Each 1–5; null means not scored yet.</summary>
public record AcceptanceScoresDto
{
    [Range(1, 5, ErrorMessage = "Puan 1 ile 5 arasında olmalıdır.")]
    public int? StrategicFit { get; init; }

    [Range(1, 5, ErrorMessage = "Puan 1 ile 5 arasında olmalıdır.")]
    public int? CommercialValue { get; init; }

    [Range(1, 5, ErrorMessage = "Puan 1 ile 5 arasında olmalıdır.")]
    public int? TechnicalFeasibility { get; init; }

    [Range(1, 5, ErrorMessage = "Puan 1 ile 5 arasında olmalıdır.")]
    public int? ResourceAndSchedule { get; init; }

    [Range(1, 5, ErrorMessage = "Puan 1 ile 5 arasında olmalıdır.")]
    public int? CustomerFit { get; init; }

    /// <summary>A high score means a low risk.</summary>
    [Range(1, 5, ErrorMessage = "Puan 1 ile 5 arasında olmalıdır.")]
    public int? RiskLevel { get; init; }
}

/// <summary>PUT .../assessment/level-answers — may be partly answered.</summary>
public record SaveLevelAnswersDto : LevelAnswersDto;

/// <summary>PUT .../assessment/acceptance-scores — may be partly scored.</summary>
public record SaveAcceptanceScoresDto : AcceptanceScoresDto;

/// <summary>Score and suggested level are null until every question is answered.</summary>
public record LevelSectionDto(
    LevelAnswersDto Answers,
    int AnsweredCount,
    int? Score,
    int? SuggestedLevel,
    int? AppliedLevel);

/// <summary>Weighted score and recommendation are null until every criterion is scored.</summary>
public record AcceptanceSectionDto(
    AcceptanceScoresDto Scores,
    int AnsweredCount,
    decimal? WeightedScore,
    AcceptanceRecommendation? Recommendation);

/// <summary>GET /api/v1/projects/{projectId}/assessment. UpdatedBy and UpdatedAt are null until something is saved.</summary>
public record ProjectAssessmentDto(
    int ProjectId,
    LevelSectionDto Level,
    AcceptanceSectionDto Acceptance,
    UserSummaryDto? UpdatedBy,
    DateTime? UpdatedAt);

public record AnswerOptionDto(int Value, string Label);

public record LevelQuestionDto(string Key, string Title, IReadOnlyList<AnswerOptionDto> Options);

public record AcceptanceCriterionDto(string Key, string Title, int Weight);

/// <summary>GET /api/v1/assessment/definition — what the screen draws; the same rules the API calculates with.</summary>
public record AssessmentDefinitionDto(
    IReadOnlyList<LevelQuestionDto> LevelQuestions,
    string LevelRule,
    IReadOnlyList<AcceptanceCriterionDto> AcceptanceCriteria,
    string AcceptanceRule);
