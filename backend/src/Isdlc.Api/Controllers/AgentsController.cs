using Isdlc.Api.Common;
using Isdlc.Application.Agents;
using Isdlc.Application.Agents.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Isdlc.Api.Controllers;

/// <summary>
/// AI assistants (ai/ service). They only suggest; nothing is saved here.
/// /api/v1/agents/status and the project sub-resource /api/v1/projects/{projectId}/meetings/suggestions.
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
}
