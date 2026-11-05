namespace Isdlc.Application.Agents;

/// <summary>
/// The meeting agent of the AI service (ai/, Python). Implemented in Infrastructure over HTTP.
/// The agent only suggests; nothing is saved by it.
/// </summary>
public interface IMeetingAgentClient
{
    /// <summary>False when no service address is configured: the feature is off.</summary>
    bool IsConfigured { get; }

    /// <summary>Configured, running and has a model (GET /health). Cached briefly; never throws.</summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken);

    /// <summary>Open issue suggestions from meeting notes. ServiceUnavailableException (503) when the service fails.</summary>
    Task<MeetingAgentResult> SuggestOpenIssuesAsync(MeetingAgentRequest request, CancellationToken cancellationToken);
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
