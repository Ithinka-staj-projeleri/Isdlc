using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Isdlc.Application.Projects;
using Isdlc.Application.Projects.Dtos;
using Isdlc.Domain.Projects;

namespace Isdlc.Application.Tests.Projects;

/// <summary>
/// The values of the company's own fields (Faz 7 · A6), without a database: every type, required, empty, inactive
/// fields and the text the skip rules see. Expectations: docs/configurability.md → Şirkete özel alanlar.
/// </summary>
public class CustomValueRulesTests
{
    private static readonly CustomFieldDefinition Contract = new("sozlesme-no", CustomFieldType.Text, [], false);
    private static readonly CustomFieldDefinition Budget = new("butce", CustomFieldType.Number, [], false);
    private static readonly CustomFieldDefinition Start = new("baslangic", CustomFieldType.Date, [], false);
    private static readonly CustomFieldDefinition Region = new("bolge", CustomFieldType.Select, ["Marmara", "Ege"], false);
    private static readonly CustomFieldDefinition Approved = new("butce-onayli", CustomFieldType.Boolean, [], true);

    private static Dictionary<string, JsonElement> Input(string json) =>
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;

    private static CustomValuesResult Apply(string inputJson, string? stored = null, params CustomFieldDefinition[] fields) =>
        CustomValueRules.Apply(fields, stored, Input(inputJson));

    [Fact]
    public void Apply_EveryTypeValid_KeepsTypedJson()
    {
        var result = Apply(
            """{ "sozlesme-no": "  S-14 ", "butce": 250000.5, "baslangic": "2026-12-01", "bolge": "ege", "butce-onayli": false }""",
            null, Contract, Budget, Start, Region, Approved);

        Assert.True(result.IsValid);
        Assert.Equal(
            """{"sozlesme-no":"S-14","butce":250000.5,"baslangic":"2026-12-01","bolge":"Ege","butce-onayli":false}""",
            result.Json);
    }

    [Theory]
    [InlineData("""{ "butce-onayli": null }""")]
    [InlineData("""{ }""")]
    [InlineData("""{ "butce-onayli": "  " }""")]
    public void Apply_RequiredFieldEmpty_IsRequiredError(string input)
    {
        var result = Apply(input, null, Approved);

        Assert.Equal(CustomValueRules.RequiredMessage, Assert.Single(result.Errors["customValues.butce-onayli"]));
    }

    [Fact]
    public void Apply_OptionalFieldsEmpty_SavesNothing()
    {
        var result = Apply("""{ "sozlesme-no": "", "butce": null }""", null, Contract, Budget);

        Assert.True(result.IsValid);
        Assert.Null(result.Json);
    }

    [Theory]
    [InlineData("sozlesme-no", "14", CustomValueRules.TextMessage)]
    [InlineData("butce", "\"çok\"", CustomValueRules.NumberMessage)]
    [InlineData("baslangic", "\"01.12.2026\"", CustomValueRules.DateMessage)]
    [InlineData("baslangic", "\"2026-02-30\"", CustomValueRules.DateMessage)]
    [InlineData("bolge", "\"Akdeniz\"", CustomValueRules.SelectMessage)]
    [InlineData("butce-onayli", "\"evet\"", CustomValueRules.BooleanMessage)]
    public void Apply_WrongType_ErrorUnderTheField(string key, string value, string message)
    {
        var result = Apply($$"""{ "butce-onayli": true, "{{key}}": {{value}} }""", null, Contract, Budget, Start, Region, Approved);

        Assert.Equal(message, Assert.Single(result.Errors["customValues." + key]));
    }

    [Fact]
    public void Apply_TextOver500_IsTooLong()
    {
        var result = Apply($$"""{ "sozlesme-no": "{{new string('a', 501)}}" }""", null, Contract);

        Assert.Equal(CustomValueRules.TextTooLongMessage, Assert.Single(result.Errors["customValues.sozlesme-no"]));
    }

    [Fact]
    public void Apply_InactiveFieldsValue_StaysAndUnknownKeysAreIgnored()
    {
        // "eski-alan" is no longer an active field: its saved value stays; "uydurma" was never a field.
        var result = Apply(
            """{ "sozlesme-no": "S-15", "eski-alan": "yeni", "uydurma": 1 }""",
            """{"sozlesme-no":"S-14","eski-alan":"eski"}""",
            Contract);

        Assert.True(result.IsValid);
        Assert.Equal("""{"sozlesme-no":"S-15","eski-alan":"eski"}""", result.Json);
    }

    [Fact]
    public void Apply_Error_KeepsTheStoredJson()
    {
        var result = Apply("""{ "butce": "x" }""", """{"butce":1}""", Budget);

        Assert.False(result.IsValid);
        Assert.Equal("""{"butce":1}""", result.Json);
    }

    [Fact]
    public void ToText_GivesTheSkipRulesPlainText()
    {
        var text = CustomValueRules.ToText("""{"butce":250000.50,"butce-onayli":true,"bolge":"Ege","baslangic":"2026-12-01"}""");

        Assert.Equal("250000.50", text["butce"]);
        Assert.Equal("true", text["butce-onayli"]);
        Assert.Equal("Ege", text["bolge"]);
        Assert.Equal("2026-12-01", text["baslangic"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    public void Read_NothingOrUnreadable_IsEmpty(string? json)
    {
        Assert.Empty(CustomValueRules.Read(json));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("""{"butce-onayli":false}""", true)]
    [InlineData("""{"butce-onayli":"evet"}""", false)]
    [InlineData("""{"sozlesme-no":"S-14"}""", false)]
    public void RequiredFieldsFilled_LooksOnlyAtRequiredFields(string? stored, bool expected)
    {
        Assert.Equal(expected, CustomValueRules.RequiredFieldsFilled([Contract, Approved], stored));
    }

    // ---- The field definition form ----

    private static List<ValidationResult> Validate(object dto)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);
        return results;
    }

    [Theory]
    [InlineData("Butce")]
    [InlineData("bütçe")]
    [InlineData("a")]
    [InlineData("butce onayli")]
    public void CreateDto_BadKey_IsAnError(string key)
    {
        var dto = new CreateCustomFieldDto { Key = key, Label = "Bütçe", Type = CustomFieldType.Number };

        Assert.Contains(Validate(dto), r => r.MemberNames.Contains(nameof(CreateCustomFieldDto.Key)));
    }

    [Theory]
    [InlineData(CustomFieldType.Select, new string[0], "Seçim alanının en az bir seçeneği olmalıdır.")]
    [InlineData(CustomFieldType.Select, new[] { "Ege", "ege" }, "Aynı seçenek iki kez yazılamaz.")]
    [InlineData(CustomFieldType.Select, new[] { "Ege", " " }, "Her seçenek 1 ile 100 karakter arasında olmalıdır.")]
    [InlineData(CustomFieldType.Text, new[] { "Ege" }, "Seçenekler yalnızca seçim alanında kullanılır.")]
    public void CreateDto_BadOptions_IsAnError(CustomFieldType type, string[] options, string message)
    {
        var dto = new CreateCustomFieldDto { Key = "bolge", Label = "Bölge", Type = type, Options = [.. options] };

        var error = Assert.Single(Validate(dto));
        Assert.Equal(message, error.ErrorMessage);
    }

    [Fact]
    public void CreateDto_SelectWithOptions_IsValid()
    {
        var dto = new CreateCustomFieldDto { Key = "bolge", Label = "Bölge", Type = CustomFieldType.Select, Options = ["Ege"] };

        Assert.Empty(Validate(dto));
    }
}
