using Isdlc.Application.Agents.Dtos;

namespace Isdlc.Application.Agents;

/// <summary>
/// AI assistants. They prepare suggestions; a person reviews them and saves through the normal screens.
/// Design: docs/ai-vision.md. Contracts: docs/open-issues-meetings-api.md and docs/risks-api.md → "Yapay zekâ önerileri".
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

    /// <summary>
    /// Risks found in a text, each with a suggested category, probability, impact and preventive action.
    /// 404 project missing, 409 project not active, 503 the AI service is off or not reachable.
    /// </summary>
    Task<RiskSuggestionsDto> SuggestRisksAsync(int projectId, SuggestRisksDto request, CancellationToken cancellationToken);

    /// <summary>
    /// Suggested probability, impact and preventive action for the risk a person is typing in the form.
    /// Same errors as SuggestRisksAsync.
    /// </summary>
    Task<RiskAssessmentSuggestionDto> SuggestRiskAssessmentAsync(
        int projectId, SuggestRiskAssessmentDto request, CancellationToken cancellationToken);
}
