using Isdlc.Application.OpenIssues;
using Isdlc.Domain.OpenIssues;

namespace Isdlc.Application.Tests.OpenIssues;

/// <summary>
/// Pure open issue rules (no database), every combination. Expectations: docs/data-model.md → OpenIssue
/// and docs/open-issues-meetings-api.md. Cross test (PLANNING.md): Dide → Erva's modules.
/// </summary>
public class OpenIssueRulesTests
{
    private static readonly DateOnly Today = new(2026, 11, 9);

    // ---- Clean ----

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("  Müşteri BT  ", "Müşteri BT")]
    public void Clean_BlankIsNullOtherwiseTrimmed(string? text, string? expected)
    {
        Assert.Equal(expected, OpenIssueRules.Clean(text));
    }

    // ---- ValidateOwner: exactly one of user and name ----

    [Theory]
    [InlineData(3, null)]
    [InlineData(3, "")]
    [InlineData(3, "   ")]
    [InlineData(null, "Müşteri BT")]
    public void ValidateOwner_ExactlyOneOwner_IsValid(int? ownerUserId, string? ownerName)
    {
        Assert.Null(OpenIssueRules.ValidateOwner(ownerUserId, ownerName));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(null, "")]
    [InlineData(null, "   ")]
    [InlineData(3, "Müşteri BT")]
    public void ValidateOwner_NoneOrBoth_ReturnsMessage(int? ownerUserId, string? ownerName)
    {
        Assert.Equal(
            "Sorumlu olarak bir kullanıcı ya da bir ad girilmelidir; ikisi birden girilemez.",
            OpenIssueRules.ValidateOwner(ownerUserId, ownerName));
    }

    // ---- IsOverdue: not closed and the due date has passed ----

    [Theory]
    [InlineData(OpenIssueStatus.Open, -1, true)]
    [InlineData(OpenIssueStatus.InProgress, -30, true)]
    [InlineData(OpenIssueStatus.Closed, -1, false)]
    [InlineData(OpenIssueStatus.Open, 0, false)] // due today is not late yet
    [InlineData(OpenIssueStatus.Open, 1, false)]
    public void IsOverdue_DependsOnStatusAndDueDate(OpenIssueStatus status, int dueInDays, bool expected)
    {
        Assert.Equal(expected, OpenIssueRules.IsOverdue(status, Today.AddDays(dueInDays), Today));
    }

    [Theory]
    [InlineData(OpenIssueStatus.Open)]
    [InlineData(OpenIssueStatus.InProgress)]
    public void IsOverdue_NoDueDate_IsNeverLate(OpenIssueStatus status)
    {
        Assert.False(OpenIssueRules.IsOverdue(status, null, Today));
    }

    // ---- ApplyStatus: ClosedAt follows the status ----

    [Fact]
    public void ApplyStatus_Closed_SetsClosedAt()
    {
        var issue = new OpenIssue { Status = OpenIssueStatus.InProgress };
        var now = new DateTime(2026, 11, 9, 10, 0, 0, DateTimeKind.Utc);

        OpenIssueRules.ApplyStatus(issue, OpenIssueStatus.Closed, now);

        Assert.Equal(OpenIssueStatus.Closed, issue.Status);
        Assert.Equal(now, issue.ClosedAt);
    }

    [Theory]
    [InlineData(OpenIssueStatus.Open)]
    [InlineData(OpenIssueStatus.InProgress)]
    public void ApplyStatus_Reopened_ClearsClosedAt(OpenIssueStatus status)
    {
        var issue = new OpenIssue { Status = OpenIssueStatus.Closed, ClosedAt = DateTime.UtcNow };

        OpenIssueRules.ApplyStatus(issue, status, DateTime.UtcNow);

        Assert.Equal(status, issue.Status);
        Assert.Null(issue.ClosedAt);
    }

    [Fact]
    public void ApplyStatus_ClosedAgain_KeepsTheFirstClosedAt()
    {
        var closedAt = new DateTime(2026, 11, 1, 9, 0, 0, DateTimeKind.Utc);
        var issue = new OpenIssue { Status = OpenIssueStatus.Closed, ClosedAt = closedAt };

        OpenIssueRules.ApplyStatus(issue, OpenIssueStatus.Closed, DateTime.UtcNow);

        Assert.Equal(closedAt, issue.ClosedAt);
    }

    // ---- Codes ----

    [Theory]
    [InlineData("AK", 1, "AK-001")]
    [InlineData("TT", 42, "TT-042")]
    [InlineData("GR", 999, "GR-999")]
    [InlineData("AK", 1000, "AK-1000")]
    public void FormatCode_PadsToThreeDigits(string prefix, int number, string expected)
    {
        Assert.Equal(expected, OpenIssueRules.FormatCode(prefix, number));
    }

    [Theory]
    [InlineData(null, 1)]
    [InlineData("AK-001", 2)]
    [InlineData("AK-009", 10)]
    [InlineData("AK-999", 1000)]
    [InlineData("AK-1000", 1001)]
    [InlineData("bozuk", 1)]
    public void NextCodeNumber_IsHighestPlusOne(string? highestCode, int expected)
    {
        Assert.Equal(expected, OpenIssueRules.NextCodeNumber(highestCode));
    }
}
