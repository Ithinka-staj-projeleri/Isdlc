using System.ComponentModel.DataAnnotations;
using Isdlc.Application.Projects.Dtos;
using Isdlc.Domain.Projects;

namespace Isdlc.Application.Tests.Projects;

/// <summary>
/// The request form's rules (shown under the form fields as Turkish 400 errors); no database needed.
/// Expectations: docs/projects-api.md → "Yazma". Cross test (PLANNING.md): Baran → Dide's modules.
/// </summary>
public class CreateProjectDtoTests
{
    public static CreateProjectDto Valid() => new()
    {
        Name = "Bayi Sipariş Portalı",
        Description = "Bayilerin sipariş vermesi ve durumunu takip etmesi.",
        Source = RequestSource.External,
        RequestChannel = RequestChannel.Written,
        RequestType = RequestType.NewProject,
        CustomerName = "Ege Gıda Dağıtım Ltd.",
        RequesterName = "Satış Direktörü",
        StartDate = new DateOnly(2026, 11, 5),
        DueDate = new DateOnly(2027, 3, 1),
    };

    private static List<ValidationResult> Validate(CreateProjectDto dto)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);
        return results;
    }

    private static ValidationResult SingleError(CreateProjectDto dto, string member)
    {
        var error = Assert.Single(Validate(dto));
        Assert.Equal([member], error.MemberNames);
        return error;
    }

    [Fact]
    public void Validate_AllFields_IsValid()
    {
        Assert.Empty(Validate(Valid()));
    }

    [Fact]
    public void Validate_OnlyRequiredFields_IsValid()
    {
        // Due date, NDA and the people are optional.
        Assert.Empty(Validate(Valid() with { DueDate = null, BusinessAnalystId = null, ProjectManagerId = null }));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")] // only spaces counts as "not given"
    public void Validate_MissingName_ReturnsTurkishErrorOnName(string name)
    {
        Assert.Equal("Proje adı zorunludur.", SingleError(Valid() with { Name = name }, nameof(CreateProjectDto.Name)).ErrorMessage);
    }

    [Fact]
    public void Validate_NameOver200_ReturnsTurkishError()
    {
        var error = SingleError(Valid() with { Name = new string('a', 201) }, nameof(CreateProjectDto.Name));
        Assert.Equal("Proje adı en fazla 200 karakter olabilir.", error.ErrorMessage);
    }

    [Fact]
    public void Validate_Name200_IsValid()
    {
        Assert.Empty(Validate(Valid() with { Name = new string('a', 200) }));
    }

    [Fact]
    public void Validate_DescriptionOver2000_ReturnsTurkishError()
    {
        var error = SingleError(Valid() with { Description = new string('a', 2001) }, nameof(CreateProjectDto.Description));
        Assert.Equal("Açıklama en fazla 2000 karakter olabilir.", error.ErrorMessage);
    }

    [Fact]
    public void Validate_MissingEveryRequiredField_ReturnsOneTurkishErrorEach()
    {
        var errors = Validate(new CreateProjectDto());

        var messages = errors.ToDictionary(e => e.MemberNames.Single(), e => e.ErrorMessage);
        Assert.Equal(new Dictionary<string, string?>
        {
            [nameof(CreateProjectDto.Name)] = "Proje adı zorunludur.",
            [nameof(CreateProjectDto.Description)] = "Açıklama zorunludur.",
            [nameof(CreateProjectDto.Source)] = "Talep kaynağı zorunludur.",
            [nameof(CreateProjectDto.RequestChannel)] = "Talep kanalı zorunludur.",
            [nameof(CreateProjectDto.RequestType)] = "Talep türü zorunludur.",
            [nameof(CreateProjectDto.CustomerName)] = "Müşteri adı zorunludur.",
            [nameof(CreateProjectDto.RequesterName)] = "Talep sahibi zorunludur.",
            [nameof(CreateProjectDto.StartDate)] = "Başlangıç tarihi zorunludur.",
        }, messages);
    }

    [Fact]
    public void Validate_DueDateBeforeStartDate_ReturnsTurkishErrorOnDueDate()
    {
        var dto = Valid() with { StartDate = new DateOnly(2026, 11, 5), DueDate = new DateOnly(2026, 11, 4) };

        var error = SingleError(dto, nameof(CreateProjectDto.DueDate));
        Assert.Equal("Bitiş tarihi başlangıç tarihinden önce olamaz.", error.ErrorMessage);
    }

    [Fact]
    public void Validate_DueDateOnStartDate_IsValid()
    {
        var day = new DateOnly(2026, 11, 5);
        Assert.Empty(Validate(Valid() with { StartDate = day, DueDate = day }));
    }

    [Fact]
    public void Validate_UpdateDto_HasTheSameRules()
    {
        var update = new UpdateProjectDto { Name = "" };

        Assert.Contains(Validate(update), e => e.ErrorMessage == "Proje adı zorunludur.");
    }
}
