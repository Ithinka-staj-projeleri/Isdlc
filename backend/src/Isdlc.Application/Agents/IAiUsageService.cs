using Isdlc.Application.Agents.Dtos;

namespace Isdlc.Application.Agents;

/// <summary>
/// The company's AI usage for the Şirket Yöneticisi (Faz 7 · A9): the month's totals and the models it can choose.
/// Contract: docs/configurability.md → Yapay zekâ modeli ve kullanımı.
/// </summary>
public interface IAiUsageService
{
    /// <summary>"2026-11"; empty: this month. 400 for another format, 403 unless a company administrator.</summary>
    Task<AiUsageSummaryDto> GetMonthAsync(string? month, CancellationToken cancellationToken);

    /// <summary>The text models the AI service offers; empty when it is off. 403 unless a company administrator.</summary>
    Task<AiModelsDto> ListModelsAsync(CancellationToken cancellationToken);
}
