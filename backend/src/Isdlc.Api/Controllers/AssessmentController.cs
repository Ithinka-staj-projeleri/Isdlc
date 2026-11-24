using Isdlc.Api.Common;
using Isdlc.Application.Assessment;
using Isdlc.Application.QuestionBank.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Isdlc.Api.Controllers;

/// <summary>
/// Seviye &amp; Uygunluk (Faz 4 · B2). The level form and the customer fit survey are read and answered on
/// /projects/{id}/questionnaires/{level|customer-fit}; this controller only applies the level. Contract: docs/assessment-api.md
/// The route is a sub-resource of projects, so it is written in full ("/" ignores the base route).
/// </summary>
public class AssessmentController(IAssessmentService assessmentService) : ApiControllerBase
{
    /// <summary>Writes the level form's level and research days to the project. 400 until every question is answered.</summary>
    [HttpPost("/api/v1/projects/{projectId:int}/assessment/apply-level")]
    [ProducesResponseType<QuestionnaireDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<QuestionnaireDto> ApplyLevel(int projectId, CancellationToken cancellationToken)
    {
        return await assessmentService.ApplyLevelAsync(projectId, cancellationToken);
    }
}
