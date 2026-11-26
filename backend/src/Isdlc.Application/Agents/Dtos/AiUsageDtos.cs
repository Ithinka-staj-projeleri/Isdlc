using Isdlc.Domain.Agents;

namespace Isdlc.Application.Agents.Dtos;

/// <summary>
/// GET /tenants/current/ai-usage?month=2026-11 (Faz 7 · A9): what the company used in a UTC month.
/// Requests counts every call, failed ones too; Limit is the company's monthly token limit (null: none).
/// </summary>
public record AiUsageSummaryDto(
    string Month,
    int Requests,
    int FailedRequests,
    long InputTokens,
    long OutputTokens,
    long AudioSeconds,
    int? Limit,
    IReadOnlyList<AiUsageByAssistantDto> ByAssistant);

/// <summary>One assistant's share of the month; Tokens is input + output.</summary>
public record AiUsageByAssistantDto(AiAssistant Assistant, int Requests, long Tokens);

/// <summary>GET /agents/models: the text models the company can choose; empty while the AI service is off.</summary>
public record AiModelsDto(IReadOnlyList<string> Models);
