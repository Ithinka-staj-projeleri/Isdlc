using System.ComponentModel.DataAnnotations;
using Isdlc.Application.Risks.Dtos;
using Isdlc.Domain.Risks;

namespace Isdlc.Application.Tests.Risks;

/// <summary>
/// Field rules of the risk request bodies; no database needed. Messages: docs/risks-api.md → DTO'lar.
/// Cross test (PLANNING.md): Erva → Baran's modules.
/// </summary>
public class RiskDtoValidationTests
{
    private static CreateRiskDto ValidCreate() => new()
    {
        Title = "ERP servis dokümantasyonu eksik",
        Category = RiskCategory.Technical,
        Probability = 3,
        Impact = 3,
        OwnerUserId = 1,
    };

    private static List<ValidationResult> Validate(object dto)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);
        return results;
    }

    private static ValidationResult SingleErrorOn(object dto, string member)
    {
        var error = Assert.Single(Validate(dto));
        Assert.Equal([member], error.MemberNames);
        return error;
    }

    [Fact]
    public void Create_RequiredFieldsOnly_IsValid()
    {
        Assert.Empty(Validate(ValidCreate()));
    }

    [Fact]
    public void Create_EmptyBody_ReportsEveryRequiredField()
    {
        var members = Validate(new CreateRiskDto()).SelectMany(e => e.MemberNames).Order();

        Assert.Equal(
            [nameof(CreateRiskDto.Category), nameof(CreateRiskDto.Impact), nameof(CreateRiskDto.OwnerUserId),
             nameof(CreateRiskDto.Probability), nameof(CreateRiskDto.Title)],
            members);
    }

    [Fact]
    public void Create_MissingTitle_HasTurkishMessage()
    {
        var error = SingleErrorOn(ValidCreate() with { Title = null }, nameof(CreateRiskDto.Title));

        Assert.Equal("Risk tanımı zorunludur.", error.ErrorMessage);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Create_ProbabilityOutOfRange_IsInvalid(int probability)
    {
        var error = SingleErrorOn(ValidCreate() with { Probability = probability }, nameof(CreateRiskDto.Probability));

        Assert.Equal("Olasılık 1 ile 5 arasında olmalıdır.", error.ErrorMessage);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Create_ImpactOutOfRange_IsInvalid(int impact)
    {
        var error = SingleErrorOn(ValidCreate() with { Impact = impact }, nameof(CreateRiskDto.Impact));

        Assert.Equal("Etki 1 ile 5 arasında olmalıdır.", error.ErrorMessage);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void Create_ProbabilityAndImpactAtTheEdges_AreValid(int value)
    {
        Assert.Empty(Validate(ValidCreate() with { Probability = value, Impact = value }));
    }

    [Fact]
    public void Create_TextLimits_AreEnforced()
    {
        Assert.Empty(Validate(ValidCreate() with
        {
            Title = new string('a', 300),
            Description = new string('a', 2000),
            Mitigation = new string('a', 2000),
        }));

        SingleErrorOn(ValidCreate() with { Title = new string('a', 301) }, nameof(CreateRiskDto.Title));
        SingleErrorOn(ValidCreate() with { Description = new string('a', 2001) }, nameof(CreateRiskDto.Description));
        SingleErrorOn(ValidCreate() with { Mitigation = new string('a', 2001) }, nameof(CreateRiskDto.Mitigation));
    }

    [Fact]
    public void Update_MissingStatus_IsInvalid()
    {
        var create = ValidCreate();
        var update = new UpdateRiskDto
        {
            Title = create.Title,
            Category = create.Category,
            Probability = create.Probability,
            Impact = create.Impact,
            OwnerUserId = create.OwnerUserId,
        };

        var error = SingleErrorOn(update, nameof(UpdateRiskDto.Status));

        Assert.Equal("Durum zorunludur.", error.ErrorMessage);
        Assert.Empty(Validate(update with { Status = RiskStatus.Closed }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Filter_CellOutOfRange_IsInvalid(int value)
    {
        var errors = Validate(new RiskFilter { Probability = value, Impact = value });

        Assert.Equal(2, errors.Count);
    }
}
