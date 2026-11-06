using Isdlc.Application.Agents.Dtos;

namespace Isdlc.Application.Agents;

/// <summary>
/// AI assistants. They prepare suggestions; a person reviews them and saves through the normal screens.
/// Design: docs/ai-vision.md. Contract: docs/open-issues-meetings-api.md → "Yapay zekâ önerileri".
/// </summary>
public interface IAgentService
{
    /// <summary>The screens show an AI button only when the service is configured and answering.</summary>
    Task<AgentStatusDto> GetStatusAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Decisions, actions and other open issues found in meeting notes, for the new minutes form.
    /// 404 project missing, 409 project not active, 503 the AI service is off or not reachable.
    /// </summary>
    Task<MeetingSuggestionsDto> SuggestMeetingItemsAsync(
        int projectId, SuggestMeetingItemsDto request, CancellationToken cancellationToken);
}
