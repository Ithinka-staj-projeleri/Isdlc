using Isdlc.Api.Common;
using Isdlc.Application.QuestionBank;
using Isdlc.Application.QuestionBank.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Isdlc.Api.Controllers;

/// <summary>
/// The Talep Analizi tab's own part (E3): /api/v1/projects/{projectId}/request-analysis.
/// The form itself is /api/v1/projects/{projectId}/questionnaires/request-analysis (QuestionnairesController).
/// </summary>
public class RequestAnalysisController(IRequestAnalysisService requestAnalysisService) : ApiControllerBase
{
    /// <summary>Questions answered "Açık Konu Var" or "Kısmen Yanıtlandı", with the open issue each was transferred to.</summary>
    [HttpGet("/api/v1/projects/{projectId:int}/request-analysis/open-items")]
    [ProducesResponseType<IReadOnlyList<RequestAnalysisOpenItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IReadOnlyList<RequestAnalysisOpenItemDto>> ListOpenItems(
        int projectId, CancellationToken cancellationToken)
    {
        return await requestAnalysisService.ListOpenItemsAsync(projectId, cancellationToken);
    }

    /// <summary>Transfers the question's open answer to Açık Konular (one record per answer).</summary>
    [HttpPost("/api/v1/projects/{projectId:int}/request-analysis/open-items/{questionId:int}/open-issue")]
    [ProducesResponseType<RequestAnalysisOpenItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<RequestAnalysisOpenItemDto> TransferToOpenIssue(
        int projectId, int questionId, CancellationToken cancellationToken)
    {
        return await requestAnalysisService.TransferToOpenIssueAsync(projectId, questionId, cancellationToken);
    }
}
