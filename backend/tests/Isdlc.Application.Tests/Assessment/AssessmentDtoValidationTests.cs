using System.ComponentModel.DataAnnotations;
using Isdlc.Application.Assessment.Dtos;

namespace Isdlc.Application.Tests.Assessment;

/// <summary>The request bodies' ranges (shown under the fields as Turkish 400 errors); no database needed.</summary>
public class AssessmentDtoValidationTests
{
    private static List<ValidationResult> Validate(object dto)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void LevelAnswers_NothingAnswered_IsValid()
    {
        // Answers can be saved half done.
        Assert.Empty(Validate(new SaveLevelAnswersDto()));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void LevelAnswers_ValueInRange_IsValid(int value)
    {
        Assert.Empty(Validate(new SaveLevelAnswersDto { Effort = value, HardwareDependency = value }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(-1)]
    public void LevelAnswers_ValueOutOfRange_ReturnsTurkishErrorOnThatField(int value)
    {
        var error = Assert.Single(Validate(new SaveLevelAnswersDto { Integrations = value }));

        Assert.Equal([nameof(LevelAnswersDto.Integrations)], error.MemberNames);
        Assert.Equal("Cevap 1 ile 3 arasında olmalıdır.", error.ErrorMessage);
    }

    [Fact]
    public void LevelAnswers_EveryQuestion_IsChecked()
    {
        var results = Validate(new SaveLevelAnswersDto
        {
            Effort = 4, ScreensAndModules = 4, Integrations = 4, UserRoles = 4, DataSensitivity = 4, HardwareDependency = 4,
        });

        Assert.Equal(6, results.Count);
    }

    [Fact]
    public void AcceptanceScores_NothingScored_IsValid()
    {
        Assert.Empty(Validate(new SaveAcceptanceScoresDto()));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void AcceptanceScores_ValueInRange_IsValid(int value)
    {
        Assert.Empty(Validate(new SaveAcceptanceScoresDto { StrategicFit = value, RiskLevel = value }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void AcceptanceScores_ValueOutOfRange_ReturnsTurkishErrorOnThatField(int value)
    {
        var error = Assert.Single(Validate(new SaveAcceptanceScoresDto { CustomerFit = value }));

        Assert.Equal([nameof(AcceptanceScoresDto.CustomerFit)], error.MemberNames);
        Assert.Equal("Puan 1 ile 5 arasında olmalıdır.", error.ErrorMessage);
    }

    [Fact]
    public void AcceptanceScores_EveryCriterion_IsChecked()
    {
        var results = Validate(new SaveAcceptanceScoresDto
        {
            StrategicFit = 6, CommercialValue = 6, TechnicalFeasibility = 6, ResourceAndSchedule = 6, CustomerFit = 6, RiskLevel = 6,
        });

        Assert.Equal(6, results.Count);
    }
}
