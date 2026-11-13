using Isdlc.Application.Projects;

namespace Isdlc.Application.Tests.Projects;

/// <summary>
/// Proje No: a plain sequence number kept as text (no database). Expectations: docs/projects-api.md → "Proje No".
/// Cross test (PLANNING.md): Baran → Dide's modules.
/// </summary>
public class ProjectCodesTests
{
    [Theory]
    [InlineData(1, "1")]
    [InlineData(19, "19")]
    [InlineData(1000, "1000")]
    public void Format_Number_IsThePlainNumber(int number, string expected)
    {
        Assert.Equal(expected, ProjectCodes.Format(number));
    }

    [Theory]
    [InlineData(null, 1)] // the first request
    [InlineData("1", 2)]
    [InlineData("19", 20)]
    [InlineData("999", 1000)]
    [InlineData("1000", 1001)]
    public void NextNumber_HighestProjectNo_IsOneMore(string? highestCode, int expected)
    {
        Assert.Equal(expected, ProjectCodes.NextNumber(highestCode));
    }

    [Theory]
    [InlineData("")]
    [InlineData("ISDLC-2026-014")] // an old-style code is not a Proje No
    [InlineData("14 / EG-01")]
    [InlineData("-5")]
    [InlineData(" 14")]
    public void NextNumber_NotAPlainNumber_StartsAt1(string highestCode)
    {
        Assert.Equal(1, ProjectCodes.NextNumber(highestCode));
    }

    [Theory]
    [InlineData("14", 1, "14 / EG-01")]
    [InlineData("14", 12, "14 / EG-12")]
    [InlineData("1000", 100, "1000 / EG-100")] // keeps growing after 99
    public void FormatAddOn_ProjectNoAndNumber_IsProjectNoSlashEg(string parentCode, int number, string expected)
    {
        Assert.Equal(expected, ProjectCodes.FormatAddOn(parentCode, number));
    }

    [Theory]
    [InlineData(null, 1)] // the project's first add-on
    [InlineData("14 / EG-01", 2)]
    [InlineData("14 / EG-99", 100)]
    [InlineData("14 / EG-100", 101)]
    [InlineData("14", 1)]
    [InlineData("14 / EG-", 1)]
    public void NextAddOnNumber_HighestAddOnCode_IsOneMore(string? highestAddOnCode, int expected)
    {
        Assert.Equal(expected, ProjectCodes.NextAddOnNumber(highestAddOnCode));
    }
}
