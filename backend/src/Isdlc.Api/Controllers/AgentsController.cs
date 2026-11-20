using Isdlc.Api.Common;
using Isdlc.Application.Agents;
using Isdlc.Application.Agents.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Isdlc.Api.Controllers;

/// <summary>
/// AI assistants (ai/ service). They only suggest; nothing is saved here.
/// /api/v1/agents/status, /api/v1/agents/request-draft and the project sub-resources .../meetings/suggestions, .../risks/suggestions
/// and .../risks/assessment-suggestion.
/// </summary>
public class AgentsController(IAgentService agentService) : ApiControllerBase
{
    /// <summary>500 MB: an hour of video fits (docs/open-issues-meetings-api.md → Ses kaydından notlar).</summary>
    private const long MaxRecordingBytes = 500L * 1024 * 1024;

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
    /// A meeting recording (audio or video, multipart field "file") as text for the minutes assistant. The recording is
    /// turned into text on the AI service's machine; it is not sent to a model provider. Can take minutes.
    /// </summary>
    [HttpPost("/api/v1/projects/{projectId:int}/meetings/transcript")]
    [RequestSizeLimit(MaxRecordingBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRecordingBytes)]
    [ProducesResponseType<MeetingTranscriptDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<MeetingTranscriptDto> TranscribeMeeting(
        int projectId, IFormFile? file, CancellationToken cancellationToken)
    {
        await using var recording = file?.OpenReadStream();
        return await agentService.TranscribeMeetingAsync(projectId, recording, file?.Length ?? 0, cancellationToken);
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

    /// <summary>
    /// The new request form filled from the e-mail a request came with. The e-mail is sent to the AI service
    /// (personal data such as e-mail and phone numbers is masked first and put back in the answer).
    /// </summary>
    [HttpPost("request-draft")]
    [ProducesResponseType<RequestDraftDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<RequestDraftDto> DraftRequest(DraftRequestFromEmailDto request, CancellationToken cancellationToken)
    {
        return await agentService.DraftRequestFromEmailAsync(request, cancellationToken);
    }
}
