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

    /// <summary>The service has speech to text installed (GET /health). Cached with IsAvailableAsync; never throws.</summary>
    Task<bool> IsTranscriptionAvailableAsync(CancellationToken cancellationToken);

    /// <summary>Open issue suggestions from meeting notes. ServiceUnavailableException (503) when the service fails.</summary>
    Task<MeetingAgentResult> SuggestOpenIssuesAsync(MeetingAgentRequest request, CancellationToken cancellationToken);

    /// <summary>Risks found in a text, scored and with a preventive action. 503 when the service fails.</summary>
    Task<RiskAgentResult> SuggestRisksAsync(RiskAgentRequest request, CancellationToken cancellationToken);

    /// <summary>Probability, impact and preventive action for one risk a person typed. 503 when the service fails.</summary>
    Task<RiskAssessmentAgentResult> AssessRiskAsync(
        RiskAssessmentAgentRequest request, CancellationToken cancellationToken);

    /// <summary>The request form filled from the e-mail a request came with. 503 when the service fails.</summary>
    Task<RequestAgentDraft> DraftRequestAsync(RequestAgentRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// A meeting recording as text, made on the AI service's machine. BusinessRuleException (400) when the file is not
    /// audio or too large; 503 when the service fails or does not answer in 30 minutes.
    /// </summary>
    Task<MeetingAgentTranscript> TranscribeAsync(Stream recording, CancellationToken cancellationToken);
}

/// <summary>What the agent gets: only the project code, the date, the current step and the notes.</summary>
public record MeetingAgentRequest(string ProjectCode, DateOnly MeetingDate, string Notes, string? CurrentStep);

/// <summary>Minutes is null when the agent could not draft them; the open issues are still usable.</summary>
public record MeetingAgentResult(
    string Summary,
    IReadOnlyList<MeetingAgentItem> Items,
    int InvalidCount,
    string ModelName,
    MeetingAgentMinutes? Minutes = null);

/// <summary>
/// The minutes' info as the agent drafts it. Type is a MeetingType name; agenda and next steps have one "- " line each.
/// A participant's role is there only when the notes give it.
/// </summary>
public record MeetingAgentMinutes(
    string Type,
    string? TypeReason,
    IReadOnlyList<MeetingAgentParticipant> Participants,
    string Agenda,
    string? Summary,
    string? NextSteps,
    IReadOnlyList<string> Warnings);

public record MeetingAgentParticipant(string Name, string? Role);

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

/// <summary>What the request agent gets: only the e-mail and today's date (for "yıl sonuna kadar" style dates).</summary>
public record RequestAgentRequest(string EmailText, DateOnly Today);

/// <summary>
/// The request form as the agent fills it; a field is null when the e-mail does not say it.
/// RequestType is a RequestType name or null.
/// </summary>
public record RequestAgentDraft(
    string? Name,
    string? Description,
    string? CustomerName,
    string? RequesterName,
    string? RequesterContact,
    string? RequestType,
    DateOnly? RequestDate,
    DateOnly? DueDate,
    string? Notes,
    IReadOnlyList<string> Warnings,
    string ModelName);

/// <summary>A recording as text: one segment per line; Truncated when it was cut to the notes limit.</summary>
public record MeetingAgentTranscript(string Text, int DurationSeconds, string Language, bool Truncated, string ModelName);
