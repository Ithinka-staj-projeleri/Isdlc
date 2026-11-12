using System.Net.Http.Json;
using System.Text.Json;
using Isdlc.Application.Agents;
using Isdlc.Application.Common.Exceptions;
using Microsoft.Extensions.Logging;

namespace Isdlc.Infrastructure.Agents;

/// <summary>Settings under "Agents" (appsettings or Agents__ServiceUrl / Agents__ServiceKey).</summary>
public class AgentOptions
{
    /// <summary>Address of the AI service (ai/, uvicorn). Empty turns the AI features off.</summary>
    public string? ServiceUrl { get; init; }

    /// <summary>Sent as X-Agent-Key; the same value as AGENT_SERVICE_KEY in ai/.env.</summary>
    public string? ServiceKey { get; init; }
}

/// <summary>Calls the agents of the AI service (ai/isdlc_ai/server.py). Any failure becomes a 503 with a Turkish message.</summary>
public class HttpAgentClient(
    HttpClient http, AgentOptions options, TimeProvider timeProvider, ILogger<HttpAgentClient> logger)
    : IAgentClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan HealthCacheTime = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan HealthTimeout = TimeSpan.FromSeconds(2);

    // The last /health answer; every page load asks for the status, the service is asked at most every 30 s.
    // A single reference, so parallel requests never see a half-written value.
    private HealthCheck? _health;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.ServiceUrl);

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            return false;
        }

        var now = timeProvider.GetUtcNow();
        if (_health is { } cached && now - cached.CheckedAt < HealthCacheTime)
        {
            return cached.Available;
        }

        var available = false;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(HealthTimeout);
            var health = await http.GetFromJsonAsync<ServiceHealth>("health", Json, timeout.Token);
            available = health?.ModelConfigured == true;
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogInformation("AI service not available at {Url}: {Message}", options.ServiceUrl, error.Message);
        }

        _health = new HealthCheck(available, now);
        return available;
    }

    public async Task<MeetingAgentResult> SuggestOpenIssuesAsync(
        MeetingAgentRequest request, CancellationToken cancellationToken)
    {
        var body = await PostAsync<ServiceResponse>(
            "v1/meeting/suggestions",
            new
            {
                projectCode = request.ProjectCode,
                meetingDate = request.MeetingDate,
                notes = request.Notes,
                currentStep = request.CurrentStep,
            },
            cancellationToken);

        return new MeetingAgentResult(
            body.Summary ?? string.Empty,
            (body.Items ?? []).Select(i => new MeetingAgentItem(
                i.Type ?? string.Empty,
                i.Title ?? string.Empty,
                i.Description,
                i.OwnerName,
                i.DueDate,
                i.Evidence ?? string.Empty,
                i.Warnings ?? [])).ToList(),
            body.InvalidCount,
            body.Run?.ModelName ?? string.Empty,
            body.Minutes is { Type: not null } m
                ? new MeetingAgentMinutes(
                    m.Type,
                    m.TypeReason,
                    (m.Participants ?? []).Select(p => new MeetingAgentParticipant(p.Name ?? string.Empty, p.Role)).ToList(),
                    m.Agenda ?? string.Empty,
                    m.Summary,
                    m.NextSteps,
                    m.Warnings ?? [])
                : null);
    }

    public async Task<RiskAgentResult> SuggestRisksAsync(RiskAgentRequest request, CancellationToken cancellationToken)
    {
        var body = await PostAsync<RiskServiceResponse>(
            "v1/risk/suggestions",
            new { projectCode = request.ProjectCode, sourceText = request.SourceText, currentStep = request.CurrentStep },
            cancellationToken);

        return new RiskAgentResult(
            (body.Items ?? []).Select(i => new RiskAgentItem(
                i.Title ?? string.Empty,
                i.Description,
                i.Category,
                i.Probability,
                i.Impact,
                i.AssessmentReason,
                i.Mitigation,
                i.Evidence,
                i.Warnings ?? [])).ToList(),
            body.InvalidCount,
            ModelNameOf(body.Runs));
    }

    public async Task<RiskAssessmentAgentResult> AssessRiskAsync(
        RiskAssessmentAgentRequest request, CancellationToken cancellationToken)
    {
        var body = await PostAsync<RiskAssessmentServiceResponse>(
            "v1/risk/assessment",
            new
            {
                projectCode = request.ProjectCode,
                title = request.Title,
                description = request.Description,
                currentStep = request.CurrentStep,
            },
            cancellationToken);

        return new RiskAssessmentAgentResult(
            body.Probability,
            body.Impact,
            body.AssessmentReason,
            body.Mitigation,
            body.Warnings ?? [],
            ModelNameOf(body.Runs));
    }

    /// <summary>Sends the request with the service key; any failure becomes a 503 with a Turkish message.</summary>
    private async Task<TResponse> PostAsync<TResponse>(string path, object payload, CancellationToken cancellationToken)
        where TResponse : class
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(payload, options: Json),
        };
        if (!string.IsNullOrWhiteSpace(options.ServiceKey))
        {
            message.Headers.Add("X-Agent-Key", options.ServiceKey);
        }

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(message, cancellationToken);
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(error, "AI service not reachable at {Url}", options.ServiceUrl);
            throw new ServiceUnavailableException("Yapay zekâ servisine şu an ulaşılamıyor. Biraz sonra tekrar deneyin.");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                // The service writes Turkish messages for the user in "detail" (model busy, quota...).
                var detail = await ReadDetailAsync(response, cancellationToken);
                logger.LogWarning("AI service answered {Status}: {Detail}", (int)response.StatusCode, detail);
                throw new ServiceUnavailableException(
                    response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable && detail is not null
                        ? detail
                        : "Yapay zekâ servisi isteği işleyemedi. Biraz sonra tekrar deneyin.");
            }

            return await response.Content.ReadFromJsonAsync<TResponse>(Json, cancellationToken)
                ?? throw new ServiceUnavailableException("Yapay zekâ servisinden boş cevap geldi.");
        }
    }

    /// <summary>The model of the last call; every task of one request uses the same model.</summary>
    private static string ModelNameOf(List<ServiceRun>? runs) => runs?.LastOrDefault()?.ModelName ?? string.Empty;

    private static async Task<string?> ReadDetailAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ServiceError>(Json, cancellationToken);
            // FastAPI's own 422 has a list in "detail"; only a plain message is passed on.
            return problem?.Detail is { ValueKind: JsonValueKind.String } detail ? detail.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // Shapes of the AI service's JSON (camelCase, see ai/isdlc_ai/server.py).
    private record ServiceResponse(
        string? Summary, List<ServiceItem>? Items, int InvalidCount, ServiceRun? Run, ServiceMinutes? Minutes);

    private record ServiceMinutes(
        string? Type,
        string? TypeReason,
        List<ServiceParticipant>? Participants,
        string? Agenda,
        string? Summary,
        string? NextSteps,
        List<string>? Warnings);

    private record ServiceParticipant(string? Name, string? Role);

    private record ServiceItem(
        string? Type, string? Title, string? Description, string? OwnerName, DateOnly? DueDate, string? Evidence, List<string>? Warnings);

    private record ServiceRun(string? ModelName);

    private record RiskServiceResponse(List<RiskServiceItem>? Items, int InvalidCount, List<ServiceRun>? Runs);

    private record RiskServiceItem(
        string? Title,
        string? Description,
        string? Category,
        int? Probability,
        int? Impact,
        string? AssessmentReason,
        string? Mitigation,
        string? Evidence,
        List<string>? Warnings);

    private record RiskAssessmentServiceResponse(
        int? Probability, int? Impact, string? AssessmentReason, string? Mitigation, List<string>? Warnings,
        List<ServiceRun>? Runs);

    private record ServiceError(JsonElement? Detail);

    private record ServiceHealth(string? Status, bool ModelConfigured);

    private sealed record HealthCheck(bool Available, DateTimeOffset CheckedAt);
}
