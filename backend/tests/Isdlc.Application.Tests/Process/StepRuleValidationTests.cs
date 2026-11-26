using Isdlc.Application.Process;
using Isdlc.Domain.Process;

namespace Isdlc.Application.Tests.Process;

/// <summary>
/// Which skip rules and prerequisites can be saved on a step (Faz 7 · A7, flow editing). No database.
/// Rules: docs/configurability.md → Atlama kuralları, Adım ön koşulları.
/// </summary>
public class StepRuleValidationTests
{
    private static readonly ConditionField[] CustomFields =
    [
        new("custom:butce-onayli", "Bütçe onaylı", ConditionFieldType.Boolean),
        new("custom:musteri-segmenti", "Müşteri segmenti", ConditionFieldType.Choice, ["Altın", "Gümüş"]),
        new("custom:butce", "Bütçe", ConditionFieldType.Number),
        new("custom:baslangic", "Başlangıç", ConditionFieldType.Date),
        new("custom:sozlesme-no", "Sözleşme no", ConditionFieldType.Text),
    ];

    private static readonly HashSet<string> Forms = ["customer-fit", "level"];
    private static readonly HashSet<string> Documents = ["PKD", "BRD"];

    private static string? Error(StepConditionRule rule) =>
        StepRuleValidation.ValidateConditions([rule], CustomFields).SingleOrDefault()?.Message;

    private static StepConditionRule Rule(string field, ConditionOperator op, string? value = null) => new(field, op, value);

    // ---- Skip rules ----

    public static TheoryData<StepConditionRule> ValidRules => new()
    {
        // The three rules every company's template has.
        Rule("source", ConditionOperator.Equals, "External"),
        Rule("requiresOffer", ConditionOperator.Equals, "true"),
        new StepConditionRule("level", ConditionOperator.AtLeast, "2", ConditionWhenEmpty.Met),
        // Every field type and comparison it allows.
        Rule("requestType", ConditionOperator.NotEquals, "Support"),
        Rule("source", ConditionOperator.Equals, "external"),
        Rule("level", ConditionOperator.AtMost, "1"),
        Rule("custom:butce-onayli", ConditionOperator.Equals, "false"),
        Rule("custom:musteri-segmenti", ConditionOperator.Equals, "Altın"),
        Rule("custom:butce", ConditionOperator.AtLeast, "100000"),
        Rule("custom:baslangic", ConditionOperator.AtMost, "2026-12-31"),
        Rule("custom:sozlesme-no", ConditionOperator.Equals, "S-14"),
        Rule("custom:sozlesme-no", ConditionOperator.IsSet),
        Rule("level", ConditionOperator.IsNotSet),
    };

    [Theory]
    [MemberData(nameof(ValidRules))]
    public void ValidateConditions_ValidRule_HasNoError(StepConditionRule rule)
    {
        Assert.Null(Error(rule));
    }

    public static TheoryData<StepConditionRule, string> InvalidRules => new()
    {
        { Rule("", ConditionOperator.Equals, "x"), "Kural alanı seçilmelidir." },
        { Rule("budget", ConditionOperator.Equals, "x"), "Bilinmeyen alan: «budget»." },
        { Rule("custom:silinmis", ConditionOperator.Equals, "x"), "Bilinmeyen alan: «custom:silinmis»." },
        { Rule("source", (ConditionOperator)99, "External"), "Geçersiz atlama kuralı." },
        { Rule("source", ConditionOperator.AtLeast, "External"), "«Talep kaynağı» için «en az» ve «en çok» kullanılamaz." },
        { Rule("custom:sozlesme-no", ConditionOperator.AtMost, "S-14"), "«Sözleşme no» için «en az» ve «en çok» kullanılamaz." },
        { Rule("level", ConditionOperator.IsSet, "2"), "«Dolu» ve «boş» karşılaştırmalarında değer girilmez." },
        { Rule("source", ConditionOperator.Equals), "Değer girilmelidir." },
        { Rule("source", ConditionOperator.Equals, "  "), "Değer girilmelidir." },
        { Rule("custom:sozlesme-no", ConditionOperator.Equals, new string('x', 101)), "Değer en fazla 100 karakter olabilir." },
        { Rule("source", ConditionOperator.Equals, "Partner"), "«Partner», «Talep kaynağı» alanının seçeneklerinden biri değil." },
        { Rule("custom:musteri-segmenti", ConditionOperator.Equals, "Bronz"), "«Bronz», «Müşteri segmenti» alanının seçeneklerinden biri değil." },
        { Rule("requiresOffer", ConditionOperator.Equals, "evet"), "Değer «Evet» ya da «Hayır» olmalıdır." },
        { Rule("custom:butce", ConditionOperator.AtLeast, "yüz bin"), "Değer bir sayı olmalıdır." },
        { Rule("level", ConditionOperator.AtLeast, "4"), "Seviye 1 ile 3 arasında olmalıdır." },
        { Rule("custom:baslangic", ConditionOperator.AtMost, "31.12.2026"), "Tarih yyyy-aa-gg biçiminde olmalıdır." },
    };

    [Theory]
    [MemberData(nameof(InvalidRules))]
    public void ValidateConditions_InvalidRule_SaysWhy(StepConditionRule rule, string expected)
    {
        Assert.Equal(expected, Error(rule));
    }

    [Fact]
    public void ValidateConditions_ErrorsKeepTheRulesIndex()
    {
        var errors = StepRuleValidation.ValidateConditions(
            [Rule("source", ConditionOperator.Equals, "External"), Rule("level", ConditionOperator.AtLeast, "9"),
             Rule("requiresOffer", ConditionOperator.Equals, "true"), Rule("x", ConditionOperator.Equals, "y")],
            CustomFields);

        Assert.Equal([1, 3], errors.Select(e => e.Index));
    }

    [Fact]
    public void ValidateConditions_TooManyRules_IsOneError()
    {
        var rules = Enumerable.Repeat(Rule("source", ConditionOperator.Equals, "External"), StepRuleValidation.MaxRules + 1).ToList();

        var error = Assert.Single(StepRuleValidation.ValidateConditions(rules, CustomFields));

        Assert.Equal("Bir adımda en fazla 10 kural olabilir.", error.Message);
    }

    [Fact]
    public void ValidateConditions_WithoutCustomFields_OnlyTheRecordsOwnFieldsAreKnown()
    {
        var error = Assert.Single(StepRuleValidation.ValidateConditions(
            [Rule("custom:butce-onayli", ConditionOperator.Equals, "true")], []));

        Assert.Equal("Bilinmeyen alan: «custom:butce-onayli».", error.Message);
    }

    [Fact]
    public void BuiltInFields_MatchWhatTheRulesRead()
    {
        // Every field StepConditions reads from the record can be chosen, and nothing else.
        Assert.Equal(["source", "requestType", "requiresOffer", "level"], StepRuleValidation.BuiltInFields.Select(f => f.Field));
    }

    // ---- Prerequisites ----

    private static IReadOnlyList<StepRuleError> Errors(params StepPrerequisite[] prerequisites) =>
        StepRuleValidation.ValidatePrerequisites(prerequisites, Forms, Documents);

    public static TheoryData<StepPrerequisite> ValidPrerequisites => new()
    {
        new(PrerequisiteKind.RequestFormComplete),
        new(PrerequisiteKind.QuestionnaireComplete, "customer-fit"),
        new(PrerequisiteKind.LevelApplied),
        new(PrerequisiteKind.MeetingExists),
        new(PrerequisiteKind.RiskExists),
        new(PrerequisiteKind.RequirementsApproved),
        new(PrerequisiteKind.DocumentApproved, "PKD"),
    };

    [Theory]
    [MemberData(nameof(ValidPrerequisites))]
    public void ValidatePrerequisites_Valid_HasNoError(StepPrerequisite prerequisite)
    {
        Assert.Empty(Errors(prerequisite));
    }

    public static TheoryData<StepPrerequisite, string> InvalidPrerequisites => new()
    {
        { new((PrerequisiteKind)99), "Geçersiz ön koşul." },
        { new(PrerequisiteKind.QuestionnaireComplete), "Bu ön koşul için form seçilmelidir." },
        { new(PrerequisiteKind.QuestionnaireComplete, "risk-analysis"), "Şirkette «risk-analysis» kodlu bir form yok." },
        { new(PrerequisiteKind.DocumentApproved, " "), "Bu ön koşul için doküman seçilmelidir." },
        { new(PrerequisiteKind.DocumentApproved, "SRS"), "Şirkette «SRS» kodlu bir doküman türü yok." },
        { new(PrerequisiteKind.DocumentApproved, new string('x', 51)), "Geçersiz ön koşul." },
        { new(PrerequisiteKind.MeetingExists, "1"), "Bu ön koşul bir değer almaz." },
    };

    [Theory]
    [MemberData(nameof(InvalidPrerequisites))]
    public void ValidatePrerequisites_Invalid_SaysWhy(StepPrerequisite prerequisite, string expected)
    {
        Assert.Equal(expected, Assert.Single(Errors(prerequisite)).Message);
    }

    [Fact]
    public void ValidatePrerequisites_SameOneTwice_IsAnErrorOnTheSecond()
    {
        var errors = Errors(
            new(PrerequisiteKind.MeetingExists),
            new(PrerequisiteKind.QuestionnaireComplete, "level"),
            new(PrerequisiteKind.MeetingExists));

        var error = Assert.Single(errors);
        Assert.Equal(2, error.Index);
        Assert.Equal("Aynı ön koşul iki kez eklenemez.", error.Message);
    }

    [Fact]
    public void ValidatePrerequisites_SameKindWithAnotherValue_IsAllowed()
    {
        Assert.Empty(Errors(new(PrerequisiteKind.DocumentApproved, "PKD"), new(PrerequisiteKind.DocumentApproved, "BRD")));
    }

    [Fact]
    public void ValidatePrerequisites_TooMany_IsOneError()
    {
        var prerequisites = Enumerable.Range(0, StepRuleValidation.MaxPrerequisites + 1)
            .Select(_ => new StepPrerequisite(PrerequisiteKind.RiskExists))
            .ToArray();

        Assert.Equal("Bir adımda en fazla 10 ön koşul olabilir.", Assert.Single(Errors(prerequisites)).Message);
    }

    [Fact]
    public void Ithinka_TemplatesRulesAndPrerequisites_AreAllValid()
    {
        // What the seed files hold must pass the same checks a company's own edits will.
        Assert.Empty(StepRuleValidation.ValidateConditions(
            [Rule("source", ConditionOperator.Equals, "External"), Rule("requiresOffer", ConditionOperator.Equals, "true"),
             new("level", ConditionOperator.AtLeast, "3", ConditionWhenEmpty.Met)], []));
        Assert.Empty(Errors(
            new(PrerequisiteKind.RequestFormComplete), new(PrerequisiteKind.QuestionnaireComplete, "customer-fit"),
            new(PrerequisiteKind.LevelApplied), new(PrerequisiteKind.MeetingExists), new(PrerequisiteKind.RiskExists)));
    }
}
