using Isdlc.Application.QuestionBank;
using Isdlc.Domain.QuestionBank;

namespace Isdlc.Application.Tests.QuestionBank;

/// <summary>Talep Analizi's pure rules (no database). Expectations: docs/question-bank-api.md → "Talep Analizi (E3)".</summary>
public class RequestAnalysisRulesTests
{
    [Theory]
    [InlineData(OptionMeaning.Open, true)]
    [InlineData(OptionMeaning.Partial, true)]
    [InlineData(OptionMeaning.Complete, false)]
    [InlineData(OptionMeaning.NotApplicable, false)]
    [InlineData(OptionMeaning.Negative, false)]
    [InlineData(null, false)]
    public void IsOpenItem_OnlyOpenAndPartial(OptionMeaning? meaning, bool expected)
    {
        Assert.Equal(expected, RequestAnalysisRules.IsOpenItem(meaning));
    }

    [Fact]
    public void Title_ShortQuestion_IsTheQuestion()
    {
        Assert.Equal("Kaç kullanıcı olacak?", RequestAnalysisRules.Title("  Kaç kullanıcı olacak?  "));
    }

    [Fact]
    public void Title_LongQuestion_IsShortenedToTheField()
    {
        var title = RequestAnalysisRules.Title(new string('a', 400));

        Assert.Equal(RequestAnalysisRules.MaxTitleLength, title.Length);
        Assert.EndsWith("…", title);
    }

    [Fact]
    public void Description_AllParts_OneLineEach()
    {
        var description = RequestAnalysisRules.Description(
            12, "Hedef Durum", "Açık Konu Var", "  Senaryolar netleşmedi.  ", "Birim müdürü dönecek.");

        Assert.Equal(
            "Talep Analizi · soru 12 (Hedef Durum): Açık Konu Var\n" +
            "Açık konu / eksik bilgi: Senaryolar netleşmedi.\n" +
            "Paydaş yanıtı: Birim müdürü dönecek.",
            description);
    }

    [Fact]
    public void Description_EmptyParts_AreLeftOut()
    {
        Assert.Equal(
            "Talep Analizi · soru 3: Kısmen Yanıtlandı",
            RequestAnalysisRules.Description(3, null, "Kısmen Yanıtlandı", "  ", null));
    }

    [Fact]
    public void Description_TooLong_IsShortenedToTheField()
    {
        var description = RequestAnalysisRules.Description(1, null, "Açık Konu Var", new string('a', 1500), new string('b', 1500));

        Assert.Equal(RequestAnalysisRules.MaxDescriptionLength, description.Length);
        Assert.EndsWith("…", description);
    }
}
