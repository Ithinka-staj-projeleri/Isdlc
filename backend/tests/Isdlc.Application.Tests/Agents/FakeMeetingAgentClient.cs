using Isdlc.Application.Agents;

namespace Isdlc.Application.Tests.Agents;

/// <summary>
/// Stands in for the AI service (ai/, Python), so no model is called. The test decides whether it is configured,
/// what it answers or whether it fails, and checks what it was sent.
/// </summary>
public sealed class FakeMeetingAgentClient : IMeetingAgentClient
{
    public bool IsConfigured { get; init; } = true;
    public bool IsAvailable { get; init; } = true;

    public MeetingAgentResult Result { get; init; } = new("Özet", [], 0, "test-model");

    /// <summary>Thrown instead of answering, like a service that is down.</summary>
    public Exception? Failure { get; init; }

    /// <summary>What the service received; null when it was never called.</summary>
    public MeetingAgentRequest? LastRequest { get; private set; }

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken) => Task.FromResult(IsAvailable);

    public Task<MeetingAgentResult> SuggestOpenIssuesAsync(MeetingAgentRequest request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        return Failure is null ? Task.FromResult(Result) : Task.FromException<MeetingAgentResult>(Failure);
    }
}
