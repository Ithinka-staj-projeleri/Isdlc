using Isdlc.Domain.Assessment;

namespace Isdlc.Application.Assessment.Dtos;

// Contract: docs/assessment-api.md → DTO'lar. These are QuestionnaireDto.Result of the two forms.

/// <summary>Section 1 of the level form. Level is null until every question of the section is answered.</summary>
public record DocumentLevelResultDto(int Score, int MaxScore, int Answered, int Listed, int? Level);

/// <summary>Section 2 of the level form. Days is null until every question of the section is answered.</summary>
public record ResearchResultDto(int Score, int MaxScore, int Answered, int Listed, int? Days);

/// <summary>Result of the level form (setCode "level"), with what is applied to the project now.</summary>
public record LevelResultDto(
    DocumentLevelResultDto DocumentLevel,
    ResearchResultDto Research,
    int? AppliedLevel,
    int? AppliedResearchDays);

/// <summary>Result of the customer fit survey (setCode "customer-fit"). Rating is null until every question is answered.</summary>
public record CustomerFitResultDto(int Score, int MaxScore, int Answered, int Listed, CustomerFitRating? Rating);
