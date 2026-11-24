using Isdlc.Application.Agents;

namespace Isdlc.Application.Tests.Agents;

/// <summary>
/// Which assistants a company may use, for every combination of its two switches; no database.
/// Expectations: docs/multi-tenancy.md → "Yapay zekâ ayarlarının etkisi".
/// </summary>
public class CompanyAiRulesTests
{
    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(true, false, false, true)]
    [InlineData(true, true, true, true)]
    public void Rules_EveryCombinationOfTheSwitches(bool aiEnabled, bool dataSharing, bool sendsText, bool transcribes)
    {
        var company = new CompanyAiRules.Settings(aiEnabled, dataSharing);

        Assert.Equal(sendsText, CompanyAiRules.MaySendText(company));
        Assert.Equal(transcribes, CompanyAiRules.MayTranscribe(company));
    }

    [Fact]
    public void Off_AllowsNothing()
    {
        Assert.False(CompanyAiRules.MaySendText(CompanyAiRules.Settings.Off));
        Assert.False(CompanyAiRules.MayTranscribe(CompanyAiRules.Settings.Off));
    }
}
