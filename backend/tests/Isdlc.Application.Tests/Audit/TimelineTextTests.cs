using Isdlc.Application.Audit;

namespace Isdlc.Application.Tests.Audit;

/// <summary>Short titles for timeline rows; no database. Expectations: docs/audit-api.md → Zaman çizelgesi.</summary>
public class TimelineTextTests
{
    [Theory]
    [InlineData("Talep alınır ve Proje No verilir. Talep alınır; listeye yazılır.", "Talep alınır ve Proje No verilir")]
    [InlineData("Proje kapatıldı.", "Proje kapatıldı")]
    [InlineData("  Canlıya alındı  ", "Canlıya alındı")]
    [InlineData("Sürüm 1.2 hazırlanır. Sonra test edilir.", "Sürüm 1.2 hazırlanır")]
    public void FirstSentence_ReturnsTheTextUpToTheFirstSentenceEnd(string text, string expected)
    {
        Assert.Equal(expected, TimelineText.FirstSentence(text));
    }

    [Theory]
    [InlineData("- Ekranlar\n- Raporlar", "Ekranlar")]
    [InlineData("\n\n  • Depo süreci\n", "Depo süreci")]
    [InlineData("Tek konu", "Tek konu")]
    [InlineData("", "")]
    public void FirstAgendaLine_ReturnsTheFirstTopicWithoutItsMark(string agenda, string expected)
    {
        Assert.Equal(expected, TimelineText.FirstAgendaLine(agenda));
    }
}
