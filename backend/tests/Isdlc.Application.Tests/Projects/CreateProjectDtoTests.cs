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
        RequestChannel = RequestChannel.Email,
        RequestType = RequestType.NewProject,
        CustomerName = "Ege Gıda Dağıtım Ltd.",
        RequesterName = "Satış Direktörü",
        RequiresOffer = true,
        RequestDate = new DateOnly(2026, 11, 5),
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
        // Due date, NDA, contact, notes and the people are optional.
        Assert.Empty(Validate(Valid() with
        {
            DueDate = null, BusinessAnalystId = null, ProjectManagerId = null, RequesterContact = null, Notes = null,
        }));
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
            [nameof(CreateProjectDto.CustomerName)] = "Talep sahibi zorunludur.",
            [nameof(CreateProjectDto.RequesterName)] = "Talebi ileten kişi zorunludur.",
            [nameof(CreateProjectDto.RequiresOffer)] = "Teklif hazırlanıp hazırlanmayacağı belirtilmelidir.",
            [nameof(CreateProjectDto.RequestDate)] = "Talep tarihi zorunludur.",
        }, messages);
    }

    [Fact]
    public void Validate_DueDateBeforeRequestDate_ReturnsTurkishErrorOnDueDate()
    {
        var dto = Valid() with { RequestDate = new DateOnly(2026, 11, 5), DueDate = new DateOnly(2026, 11, 4) };

        var error = SingleError(dto, nameof(CreateProjectDto.DueDate));
        Assert.Equal("Bitiş tarihi talep tarihinden önce olamaz.", error.ErrorMessage);
    }

    [Fact]
    public void Validate_DueDateOnRequestDate_IsValid()
    {
        var day = new DateOnly(2026, 11, 5);
        Assert.Empty(Validate(Valid() with { RequestDate = day, DueDate = day }));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Validate_RequiresOfferYesOrNo_IsValid(bool requiresOffer)
    {
        Assert.Empty(Validate(Valid() with { RequiresOffer = requiresOffer }));
    }

    [Fact]
    public void Validate_RequiresOfferNotGiven_ReturnsTurkishError()
    {
        // Not silently "no offer": that would skip the offer steps of the flow.
        var error = SingleError(Valid() with { RequiresOffer = null }, nameof(CreateProjectDto.RequiresOffer));
        Assert.Equal("Teklif hazırlanıp hazırlanmayacağı belirtilmelidir.", error.ErrorMessage);
    }

    [Theory]
    [InlineData(RequestChannel.Email)]
    [InlineData(RequestChannel.Phone)]
    [InlineData(RequestChannel.Meeting)]
    public void Validate_ChannelOfTheForm_IsValidWithoutOtherText(RequestChannel channel)
    {
        Assert.Empty(Validate(Valid() with { RequestChannel = channel, RequestChannelOther = null }));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void Validate_OtherChannelWithoutText_ReturnsTurkishError(string? other)
    {
        // The form's "Diğer: ____" has to be filled in.
        var dto = Valid() with { RequestChannel = RequestChannel.Other, RequestChannelOther = other };

        var error = SingleError(dto, nameof(CreateProjectDto.RequestChannelOther));
        Assert.Equal("Diğer talep kanalı yazılmalıdır.", error.ErrorMessage);
    }

    [Fact]
    public void Validate_OtherChannelWithText_IsValid()
    {
        Assert.Empty(Validate(Valid() with { RequestChannel = RequestChannel.Other, RequestChannelOther = "WhatsApp" }));
    }

    [Fact]
    public void Validate_OtherChannelOver200_ReturnsTurkishError()
    {
        var dto = Valid() with { RequestChannel = RequestChannel.Other, RequestChannelOther = new string('a', 201) };

        var error = SingleError(dto, nameof(CreateProjectDto.RequestChannelOther));
        Assert.Equal("Diğer talep kanalı en fazla 200 karakter olabilir.", error.ErrorMessage);
    }

    [Fact]
    public void Validate_AddOnWithoutProject_ReturnsTurkishErrorOnParentProjectId()
    {
        var dto = Valid() with { RequestType = RequestType.AdditionalModule, ParentProjectId = null };

        var error = SingleError(dto, nameof(CreateProjectDto.ParentProjectId));
        Assert.Equal("Ek geliştirmenin ait olduğu proje seçilmelidir.", error.ErrorMessage);
    }

    [Fact]
    public void Validate_ProjectWithAnotherRequestType_ReturnsTurkishErrorOnRequestType()
    {
        // A request that belongs to a project is an add-on, whatever type was chosen.
        var dto = Valid() with { RequestType = RequestType.NewProject, ParentProjectId = 14 };

        var error = SingleError(dto, nameof(CreateProjectDto.RequestType));
        Assert.Equal("Bir projeye bağlı talep ek geliştirmedir; talep türü \"Ek geliştirme\" olmalıdır.", error.ErrorMessage);
    }

    [Fact]
    public void Validate_AddOnWithProject_IsValid()
    {
        Assert.Empty(Validate(Valid() with { RequestType = RequestType.AdditionalModule, ParentProjectId = 14 }));
    }

    [Fact]
    public void Validate_ContactOver200_ReturnsTurkishError()
    {
        var error = SingleError(Valid() with { RequesterContact = new string('a', 201) }, nameof(CreateProjectDto.RequesterContact));
        Assert.Equal("İletişim bilgisi en fazla 200 karakter olabilir.", error.ErrorMessage);
    }

    [Fact]
    public void Validate_NotesOver2000_ReturnsTurkishError()
    {
        var error = SingleError(Valid() with { Notes = new string('a', 2001) }, nameof(CreateProjectDto.Notes));
        Assert.Equal("Notlar en fazla 2000 karakter olabilir.", error.ErrorMessage);
    }

    [Fact]
    public void Validate_UpdateDto_HasTheSameRules()
    {
        var update = new UpdateProjectDto { Name = "" };

        Assert.Contains(Validate(update), e => e.ErrorMessage == "Proje adı zorunludur.");
    }
}
