using Isdlc.Application.Agents;

namespace Isdlc.Application.Tests.Agents;

/// <summary>
/// Stands in for the AI service (ai/, Python), so no model is called. The test decides whether it is configured,
/// what it answers or whether it fails, and checks what it was sent.
/// </summary>
public sealed class FakeAgentClient : IAgentClient
{
    public bool IsConfigured { get; init; } = true;
    public bool IsAvailable { get; init; } = true;
    public bool IsTranscriptionAvailable { get; init; } = true;

    public MeetingAgentResult Result { get; init; } = new("Özet", [], 0, "test-model");

    public RiskAgentResult RiskResult { get; init; } = new([], 0, "test-model");

    public RiskAssessmentAgentResult RiskAssessmentResult { get; init; } =
        new(3, 4, "Gerekçe.", "Önleyici aksiyon.", [], "test-model");

    public RequestAgentDraft RequestDraft { get; init; } =
        new("Talep", "Açıklama.", null, null, null, null, null, null, null, [], "test-model");

    public MeetingAgentTranscript Transcript { get; init; } = new("Merhaba.\nBaşlayalım.", 61, "tr", false, "test-whisper");

    /// <summary>Thrown instead of answering, like a service that is down.</summary>
    public Exception? Failure { get; init; }

    /// <summary>What the service received; null when it was never called.</summary>
    public MeetingAgentRequest? LastRequest { get; private set; }

    public RiskAgentRequest? LastRiskRequest { get; private set; }

    public RiskAssessmentAgentRequest? LastRiskAssessmentRequest { get; private set; }

    public RequestAgentRequest? LastRequestDraftRequest { get; private set; }

    /// <summary>The bytes of the last recording sent; null when none was.</summary>
    public byte[]? LastRecording { get; private set; }

    public bool WasCalled =>
        LastRequest is not null || LastRiskRequest is not null || LastRiskAssessmentRequest is not null
        || LastRequestDraftRequest is not null || LastRecording is not null;

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken) => Task.FromResult(IsAvailable);

    public Task<bool> IsTranscriptionAvailableAsync(CancellationToken cancellationToken) =>
        Task.FromResult(IsTranscriptionAvailable);

    public Task<MeetingAgentResult> SuggestOpenIssuesAsync(MeetingAgentRequest request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        return Answer(Result);
    }

    public Task<RiskAgentResult> SuggestRisksAsync(RiskAgentRequest request, CancellationToken cancellationToken)
    {
        LastRiskRequest = request;
        return Answer(RiskResult);
    }

    public Task<RiskAssessmentAgentResult> AssessRiskAsync(
        RiskAssessmentAgentRequest request, CancellationToken cancellationToken)
    {
        LastRiskAssessmentRequest = request;
        return Answer(RiskAssessmentResult);
    }

    public Task<RequestAgentDraft> DraftRequestAsync(RequestAgentRequest request, CancellationToken cancellationToken)
    {
        LastRequestDraftRequest = request;
        return Answer(RequestDraft);
    }

    public async Task<MeetingAgentTranscript> TranscribeAsync(Stream recording, CancellationToken cancellationToken)
    {
        using var copy = new MemoryStream();
        await recording.CopyToAsync(copy, cancellationToken);
        LastRecording = copy.ToArray();
        return await Answer(Transcript);
    }

    private Task<T> Answer<T>(T result) => Failure is null ? Task.FromResult(result) : Task.FromException<T>(Failure);
}
