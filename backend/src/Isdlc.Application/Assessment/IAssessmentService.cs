using Isdlc.Application.Assessment.Dtos;

namespace Isdlc.Application.Assessment;

/// <summary>
/// Level questions (flow 2.4) and acceptance scoring (flow 3.1) of a project. Contract: docs/assessment-api.md
/// Common errors: 404 project missing, 409 project not active (writes only).
/// </summary>
public interface IAssessmentService
{
    /// <summary>The questions, criteria, weights and rules the screen draws.</summary>
    AssessmentDefinitionDto GetDefinition();

    /// <summary>Empty answers when nothing has been saved yet.</summary>
    Task<ProjectAssessmentDto> GetAsync(int projectId, CancellationToken cancellationToken);

    /// <summary>Saves the level answers (may be partly answered). Never changes Project.Level.</summary>
    Task<ProjectAssessmentDto> SaveLevelAnswersAsync(
        int projectId, SaveLevelAnswersDto request, CancellationToken cancellationToken);

    /// <summary>Saves the acceptance scores (may be partly scored).</summary>
    Task<ProjectAssessmentDto> SaveAcceptanceScoresAsync(
        int projectId, SaveAcceptanceScoresDto request, CancellationToken cancellationToken);

    /// <summary>Writes the suggested level to Project.Level. 400 until every level question is answered.</summary>
    Task<ProjectAssessmentDto> ApplyLevelAsync(int projectId, CancellationToken cancellationToken);
}
