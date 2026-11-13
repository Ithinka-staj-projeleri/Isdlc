using System.ComponentModel.DataAnnotations;
using Isdlc.Application.Process.Dtos;
using Isdlc.Application.Tests.Common;
using Isdlc.Domain.Projects;

namespace Isdlc.Application.Tests.Process;

/// <summary>
/// The Talep Ret / Erteleme Formu's rules (Turkish 400 errors under the fields); no database needed.
/// Expectations: docs/process-api.md → "Ret / erteleme kararı".
/// </summary>
public class RequestDecisionFormDtoTests
{
    private static List<ValidationResult> Validate(object dto)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void Validate_FilledInForm_IsValid()
    {
        Assert.Empty(Validate(TestDecisions.Form()));
    }

    [Fact]
    public void Validate_EmptyForm_ReturnsOneTurkishErrorPerRequiredField()
    {
        var messages = Validate(new RequestDecisionFormDto()).ToDictionary(e => e.MemberNames.Single(), e => e.ErrorMessage);

        Assert.Equal(new Dictionary<string, string?>
        {
            [nameof(RequestDecisionFormDto.DecisionDate)] = "Karar tarihi zorunludur.",
            [nameof(RequestDecisionFormDto.DecidedBy)] = "Kararı veren kişi ya da kurul zorunludur.",
            [nameof(RequestDecisionFormDto.Reasons)] = "En az bir gerekçe kategorisi seçilmelidir.",
            [nameof(RequestDecisionFormDto.Explanation)] = "Gerekçe açıklaması zorunludur.",
        }, messages);
    }

    [Fact]
    public void Validate_NoReasonChosen_ReturnsTurkishError()
    {
        var error = Assert.Single(Validate(TestDecisions.Form() with { Reasons = [] }));

        Assert.Equal("En az bir gerekçe kategorisi seçilmelidir.", error.ErrorMessage);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void Validate_OtherWithoutItsText_ReturnsTurkishError(string? other)
    {
        var dto = TestDecisions.Form() with { Reasons = [DecisionReason.Other], ReasonOther = other };

        var error = Assert.Single(Validate(dto));
        Assert.Equal([nameof(RequestDecisionFormDto.ReasonOther)], error.MemberNames);
        Assert.Equal("Diğer gerekçe yazılmalıdır.", error.ErrorMessage);
    }

    [Fact]
    public void Validate_SeveralReasons_AreValid()
    {
        var dto = TestDecisions.Form() with
        {
            Reasons = [DecisionReason.BudgetConstraint, DecisionReason.Prioritization, DecisionReason.Other],
            ReasonOther = "Yıl sonu bütçe dönemi",
        };

        Assert.Empty(Validate(dto));
    }

    [Fact]
    public void Validate_ExplanationOver2000_ReturnsTurkishError()
    {
        var error = Assert.Single(Validate(TestDecisions.Form(new string('a', 2001))));

        Assert.Equal("Gerekçe açıklaması en fazla 2000 karakter olabilir.", error.ErrorMessage);
    }

    [Fact]
    public void Validate_PostponeWithoutReevaluationDate_ReturnsTurkishError()
    {
        var dto = new PostponeProjectDto { ExpectedStepId = 1, Decision = TestDecisions.Form() };

        var error = Assert.Single(Validate(dto));
        Assert.Equal("Yeniden değerlendirme tarihi zorunludur.", error.ErrorMessage);
    }

    [Theory]
    [InlineData(0)]  // the same day
    [InlineData(-1)] // before the decision
    public void Validate_ReevaluationNotAfterTheDecision_ReturnsTurkishError(int days)
    {
        var dto = new PostponeProjectDto
        {
            ExpectedStepId = 1,
            Decision = TestDecisions.Form(),
            ReevaluationDate = TestDecisions.DecisionDate.AddDays(days),
        };

        var error = Assert.Single(Validate(dto));
        Assert.Equal("Yeniden değerlendirme tarihi karar tarihinden sonra olmalıdır.", error.ErrorMessage);
    }

    [Fact]
    public void Validate_RejectWithoutTheForm_ReturnsTurkishError()
    {
        var error = Assert.Single(Validate(new RejectProjectDto { ExpectedStepId = 1 }));

        Assert.Equal("Karar bilgileri girilmelidir.", error.ErrorMessage);
    }
}
