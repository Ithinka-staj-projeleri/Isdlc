using Isdlc.Api.Common;
using Isdlc.Application.Assessment;
using Isdlc.Application.Assessment.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Isdlc.Api.Controllers;

/// <summary>
/// Level questions (flow 2.4) and acceptance scoring (flow 3.1). Contract: docs/assessment-api.md
/// The project routes are a sub-resource of projects, so they are written in full ("/" ignores the base route).
/// </summary>
public class AssessmentController(IAssessmentService assessmentService) : ApiControllerBase
{
    private const string ProjectRoute = "/api/v1/projects/{projectId:int}/assessment";

    /// <summary>Questions, options, criteria, weights and rules for the screen.</summary>
    [HttpGet("/api/v1/assessment/definition")]
    [ProducesResponseType<AssessmentDefinitionDto>(StatusCodes.Status200OK)]
    public AssessmentDefinitionDto Definition()
    {
        return assessmentService.GetDefinition();
    }

    /// <summary>The project's assessment; empty answers when nothing has been saved yet.</summary>
    [HttpGet(ProjectRoute)]
    [ProducesResponseType<ProjectAssessmentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ProjectAssessmentDto> Get(int projectId, CancellationToken cancellationToken)
    {
        return await assessmentService.GetAsync(projectId, cancellationToken);
    }

    /// <summary>Saves the level answers (null = not answered). Does not change the project's level.</summary>
    [HttpPut(ProjectRoute + "/level-answers")]
    [ProducesResponseType<ProjectAssessmentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ProjectAssessmentDto> SaveLevelAnswers(
        int projectId, SaveLevelAnswersDto request, CancellationToken cancellationToken)
    {
        return await assessmentService.SaveLevelAnswersAsync(projectId, request, cancellationToken);
    }

    /// <summary>Saves the acceptance scores (null = not scored).</summary>
    [HttpPut(ProjectRoute + "/acceptance-scores")]
    [ProducesResponseType<ProjectAssessmentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ProjectAssessmentDto> SaveAcceptanceScores(
        int projectId, SaveAcceptanceScoresDto request, CancellationToken cancellationToken)
    {
        return await assessmentService.SaveAcceptanceScoresAsync(projectId, request, cancellationToken);
    }

    /// <summary>Writes the suggested level to the project. 400 until every level question is answered.</summary>
    [HttpPost(ProjectRoute + "/apply-level")]
    [ProducesResponseType<ProjectAssessmentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ProjectAssessmentDto> ApplyLevel(int projectId, CancellationToken cancellationToken)
    {
        return await assessmentService.ApplyLevelAsync(projectId, cancellationToken);
    }
}
