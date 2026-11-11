namespace Isdlc.Application.Agents;

/// <summary>
/// The agents of the AI service (ai/, Python). Implemented in Infrastructure over HTTP.
/// The agents only suggest; nothing is saved by them.
/// </summary>
public interface IAgentClient
{
    /// <summary>False when no service address is configured: the feature is off.</summary>
    bool IsConfigured { get; }

    /// <summary>Configured, running and has a model (GET /health). Cached briefly; never throws.</summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken);

    /// <summary>Open issue suggestions from meeting notes. ServiceUnavailableException (503) when the service fails.</summary>
    Task<MeetingAgentResult> SuggestOpenIssuesAsync(MeetingAgentRequest request, CancellationToken cancellationToken);

    /// <summary>Risks found in a text, scored and with a preventive action. 503 when the service fails.</summary>
    Task<RiskAgentResult> SuggestRisksAsync(RiskAgentRequest request, CancellationToken cancellationToken);

    /// <summary>Probability, impact and preventive action for one risk a person typed. 503 when the service fails.</summary>
    Task<RiskAssessmentAgentResult> AssessRiskAsync(
        RiskAssessmentAgentRequest request, CancellationToken cancellationToken);
}

/// <summary>What the agent gets: only the project code, the date, the current step and the notes.</summary>
public record MeetingAgentRequest(string ProjectCode, DateOnly MeetingDate, string Notes, string? CurrentStep);

public record MeetingAgentResult(string Summary, IReadOnlyList<MeetingAgentItem> Items, int InvalidCount, string ModelName);

/// <summary>One suggestion as the agent returns it. Type is an OpenIssueType name ("Action", "Decision", ...).</summary>
public record MeetingAgentItem(
    string Type,
    string Title,
    string? Description,
    string? OwnerName,
    DateOnly? DueDate,
    string Evidence,
    IReadOnlyList<string> Warnings);

/// <summary>What the risk agent gets: only the project code, the current step and the text.</summary>
public record RiskAgentRequest(string ProjectCode, string SourceText, string? CurrentStep);

public record RiskAgentResult(IReadOnlyList<RiskAgentItem> Items, int InvalidCount, string ModelName);

/// <summary>
/// One risk as the agent returns it. Category is a RiskCategory name or null; probability and impact are null
/// when the model did not score the risk (the warnings say why).
/// </summary>
public record RiskAgentItem(
    string Title,
    string? Description,
    string? Category,
    int? Probability,
    int? Impact,
    string? AssessmentReason,
    string? Mitigation,
    string? Evidence,
    IReadOnlyList<string> Warnings);

/// <summary>One risk a person is typing: only its title and description, the project code and the current step.</summary>
public record RiskAssessmentAgentRequest(string ProjectCode, string Title, string? Description, string? CurrentStep);

public record RiskAssessmentAgentResult(
    int? Probability,
    int? Impact,
    string? AssessmentReason,
    string? Mitigation,
    IReadOnlyList<string> Warnings,
    string ModelName);
