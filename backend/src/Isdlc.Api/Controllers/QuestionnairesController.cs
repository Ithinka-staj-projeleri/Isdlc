using Isdlc.Api.Common;
using Isdlc.Application.QuestionBank;
using Isdlc.Application.QuestionBank.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Isdlc.Api.Controllers;

/// <summary>
/// The forms filled in on a project: /api/v1/projects/{projectId}/questionnaires/{setCode}. Contract: docs/question-bank-api.md
/// It is a sub-resource of projects, so the routes are written in full ("/" ignores the base route).
/// </summary>
public class QuestionnairesController(IQuestionnaireService questionnaireService) : ApiControllerBase
{
    private const string Route = "/api/v1/projects/{projectId:int}/questionnaires/{setCode}";

    /// <summary>The form with its questions, answers and summary; empty answers when nothing was saved yet.</summary>
    [HttpGet(Route)]
    [ProducesResponseType<QuestionnaireDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<QuestionnaireDto> Get(int projectId, string setCode, CancellationToken cancellationToken)
    {
        return await questionnaireService.GetAsync(projectId, setCode, cancellationToken);
    }

    /// <summary>Saves the header: subcategory, participants, date, approver.</summary>
    [HttpPut(Route)]
    [ProducesResponseType<QuestionnaireDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<QuestionnaireDto> Save(
        int projectId, string setCode, SaveQuestionnaireDto request, CancellationToken cancellationToken)
    {
        return await questionnaireService.SaveAsync(projectId, setCode, request, cancellationToken);
    }

    /// <summary>Saves one answer; every field empty empties it.</summary>
    [HttpPut(Route + "/answers/{questionId:int}")]
    [ProducesResponseType<QuestionnaireDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<QuestionnaireDto> SaveAnswer(
        int projectId, string setCode, int questionId, SaveAnswerDto request, CancellationToken cancellationToken)
    {
        return await questionnaireService.SaveAnswerAsync(projectId, setCode, questionId, request, cancellationToken);
    }
}
