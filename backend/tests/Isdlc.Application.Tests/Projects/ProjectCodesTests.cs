using Isdlc.Application.Projects;

namespace Isdlc.Application.Tests.Projects;

/// <summary>
/// Project codes "ISDLC-{year}-{number}" (no database). Expectations: docs/projects-api.md → "Proje kodu".
/// Cross test (PLANNING.md): Baran → Dide's modules.
/// </summary>
public class ProjectCodesTests
{
    [Fact]
    public void YearPrefix_Year_IsIsdlcYearAndDash()
    {
        Assert.Equal("ISDLC-2026-", ProjectCodes.YearPrefix(2026));
    }

    [Theory]
    [InlineData(1, "ISDLC-2026-001")]
    [InlineData(19, "ISDLC-2026-019")]
    [InlineData(999, "ISDLC-2026-999")]
    [InlineData(1000, "ISDLC-2026-1000")] // keeps growing after 999
    public void Format_Number_IsPaddedTo3Digits(int number, string expected)
    {
        Assert.Equal(expected, ProjectCodes.Format(2026, number));
    }

    [Theory]
    [InlineData(null, 1)] // first request of the year
    [InlineData("ISDLC-2026-001", 2)]
    [InlineData("ISDLC-2026-019", 20)]
    [InlineData("ISDLC-2026-999", 1000)]
    [InlineData("ISDLC-2026-1000", 1001)]
    public void NextNumber_HighestCodeOfYear_IsOneMore(string? highestCode, int expected)
    {
        Assert.Equal(expected, ProjectCodes.NextNumber(highestCode));
    }

    [Theory]
    [InlineData("ISDLC-2026-")]
    [InlineData("ISDLC-2026-abc")]
    public void NextNumber_CodeWithoutNumber_StartsAt1(string highestCode)
    {
        Assert.Equal(1, ProjectCodes.NextNumber(highestCode));
    }
}
