using Isdlc.Api.Common;
using Isdlc.Application.Agents;
using Isdlc.Application.Agents.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Isdlc.Api.Controllers;

/// <summary>
/// AI assistants (ai/ service). They only suggest; nothing is saved here.
/// /api/v1/agents/status and the project sub-resources .../meetings/suggestions, .../risks/suggestions
/// and .../risks/assessment-suggestion.
/// </summary>
public class AgentsController(IAgentService agentService) : ApiControllerBase
{
    /// <summary>Which AI features the screens may offer (off when the service is not configured or not running).</summary>
    [HttpGet("status")]
    [ProducesResponseType<AgentStatusDto>(StatusCodes.Status200OK)]
    public async Task<AgentStatusDto> Status(CancellationToken cancellationToken) =>
        await agentService.GetStatusAsync(cancellationToken);

    /// <summary>
    /// Decisions, actions and other open issues found in raw meeting notes, for the new minutes form.
    /// The notes are sent to the AI service (personal data such as e-mail and phone numbers is masked first).
    /// </summary>
    [HttpPost("/api/v1/projects/{projectId:int}/meetings/suggestions")]
    [ProducesResponseType<MeetingSuggestionsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<MeetingSuggestionsDto> SuggestMeetingItems(
        int projectId, SuggestMeetingItemsDto request, CancellationToken cancellationToken)
    {
        return await agentService.SuggestMeetingItemsAsync(projectId, request, cancellationToken);
    }

    /// <summary>
    /// Risks found in a text (meeting notes, an e-mail...), each scored and with a preventive action.
    /// The text is sent to the AI service (personal data such as e-mail and phone numbers is masked first).
    /// </summary>
    [HttpPost("/api/v1/projects/{projectId:int}/risks/suggestions")]
    [ProducesResponseType<RiskSuggestionsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<RiskSuggestionsDto> SuggestRisks(
        int projectId, SuggestRisksDto request, CancellationToken cancellationToken)
    {
        return await agentService.SuggestRisksAsync(projectId, request, cancellationToken);
    }

    /// <summary>Suggested probability, impact and preventive action for the risk being typed in the form.</summary>
    [HttpPost("/api/v1/projects/{projectId:int}/risks/assessment-suggestion")]
    [ProducesResponseType<RiskAssessmentSuggestionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<RiskAssessmentSuggestionDto> SuggestRiskAssessment(
        int projectId, SuggestRiskAssessmentDto request, CancellationToken cancellationToken)
    {
        return await agentService.SuggestRiskAssessmentAsync(projectId, request, cancellationToken);
    }
}
