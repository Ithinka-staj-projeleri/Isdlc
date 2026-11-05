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

/// <summary>Calls POST /v1/meeting/suggestions of the AI service. Any failure becomes a 503 with a Turkish message.</summary>
public class HttpMeetingAgentClient(
    HttpClient http, AgentOptions options, TimeProvider timeProvider, ILogger<HttpMeetingAgentClient> logger)
    : IMeetingAgentClient
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
        using var message = new HttpRequestMessage(HttpMethod.Post, "v1/meeting/suggestions")
        {
            Content = JsonContent.Create(
                new
                {
                    projectCode = request.ProjectCode,
                    meetingDate = request.MeetingDate,
                    notes = request.Notes,
                    currentStep = request.CurrentStep,
                },
                options: Json),
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

            var body = await response.Content.ReadFromJsonAsync<ServiceResponse>(Json, cancellationToken)
                ?? throw new ServiceUnavailableException("Yapay zekâ servisinden boş cevap geldi.");

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
                body.Run?.ModelName ?? string.Empty);
        }
    }

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
    private record ServiceResponse(string? Summary, List<ServiceItem>? Items, int InvalidCount, ServiceRun? Run);

    private record ServiceItem(
        string? Type, string? Title, string? Description, string? OwnerName, DateOnly? DueDate, string? Evidence, List<string>? Warnings);

    private record ServiceRun(string? ModelName);

    private record ServiceError(JsonElement? Detail);

    private record ServiceHealth(string? Status, bool ModelConfigured);

    private sealed record HealthCheck(bool Available, DateTimeOffset CheckedAt);
}
