using System.Globalization;
using Isdlc.Domain.Agents;
using Microsoft.EntityFrameworkCore;

namespace Isdlc.Application.Agents;

/// <summary>A UTC calendar month: [Start, End).</summary>
public readonly record struct UsageMonth(DateTime Start, DateTime End)
{
    /// <summary>"2026-11".</summary>
    public override string ToString() => Start.ToString("yyyy-MM", CultureInfo.InvariantCulture);
}

/// <summary>
/// The company's AI usage by month and its monthly limit (Faz 7 · A9). The limit counts input + output tokens in the
/// UTC calendar month. Rules: docs/configurability.md → Yapay zekâ modeli ve kullanımı.
/// </summary>
public static class AiUsageRules
{
    public const string LimitReachedMessage = "Şirketinizin bu ayki yapay zekâ kullanım sınırı doldu.";
    public const string MonthFormatMessage = "Ay yyyy-AA biçiminde olmalıdır (ör. 2026-11).";

    /// <summary>The UTC month the moment falls in.</summary>
    public static UsageMonth MonthOf(DateTime utc)
    {
        var start = new DateTime(utc.Year, utc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        return new UsageMonth(start, start.AddMonths(1));
    }

    /// <summary>"2026-11" as a month; null when it is not one.</summary>
    public static UsageMonth? ParseMonth(string? text) =>
        DateTime.TryParseExact(text, "yyyy-MM", CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed)
            ? MonthOf(DateTime.SpecifyKind(parsed, DateTimeKind.Utc))
            : null;

    /// <summary>Input + output tokens the company used in the month (the query filter keeps it to the company).</summary>
    public static async Task<long> TokensInMonthAsync(
        this IQueryable<AiUsage> usages, UsageMonth month, CancellationToken cancellationToken) =>
        await usages
            .Where(u => u.CreatedAt >= month.Start && u.CreatedAt < month.End)
            .SumAsync(u => (long?)((u.InputTokens ?? 0) + (u.OutputTokens ?? 0)), cancellationToken) ?? 0;
}
