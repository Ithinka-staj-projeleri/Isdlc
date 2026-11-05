using Isdlc.Domain.OpenIssues;

namespace Isdlc.Application.OpenIssues;

/// <summary>
/// Pure rules for open issues, shared with Meetings (meeting actions become open issues).
/// No database access, so they can be unit tested directly. Rules: docs/data-model.md → OpenIssue.
/// </summary>
public static class OpenIssueRules
{
    public const string CodePrefix = "AK";

    /// <summary>Blank text counts as "not given".</summary>
    public static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    /// <summary>Exactly one owner: a user or a name. Returns the error message, or null when valid.</summary>
    public static string? ValidateOwner(int? ownerUserId, string? ownerName) =>
        (ownerUserId is null) == (Clean(ownerName) is null)
            ? "Sorumlu olarak bir kullanıcı ya da bir ad girilmelidir; ikisi birden girilemez."
            : null;

    /// <summary>Not closed and the due date has passed.</summary>
    public static bool IsOverdue(OpenIssueStatus status, DateOnly? dueDate, DateOnly today) =>
        status != OpenIssueStatus.Closed && dueDate is { } due && due < today;

    /// <summary>Sets the status and keeps ClosedAt in line with it.</summary>
    public static void ApplyStatus(OpenIssue issue, OpenIssueStatus status, DateTime utcNow)
    {
        if (issue.Status == status)
        {
            return;
        }

        issue.Status = status;
        issue.ClosedAt = status == OpenIssueStatus.Closed ? utcNow : null;
    }

    /// <summary>"AK-001": the prefix and a number padded to 3 digits (it keeps growing after 999).</summary>
    public static string FormatCode(string prefix, int number) => $"{prefix}-{number:D3}";

    /// <summary>The number after the highest existing code; 1 when there is none.</summary>
    public static int NextCodeNumber(string? highestCode) =>
        highestCode is not null && int.TryParse(highestCode[(highestCode.IndexOf('-') + 1)..], out var number)
            ? number + 1
            : 1;
}
