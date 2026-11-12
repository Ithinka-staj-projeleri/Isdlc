using System.ComponentModel.DataAnnotations;
using Isdlc.Application.QuestionBank.Dtos;
using Isdlc.Domain.Projects;

namespace Isdlc.Application.Tests.QuestionBank;

/// <summary>
/// Field rules of the question bank request bodies; no database needed. Messages: docs/question-bank-api.md → DTO'lar.
/// Cross test (PLANNING.md): Erva → Baran's modules.
/// </summary>
public class QuestionBankDtoValidationTests
{
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

    // ---- SaveAnswerDto ----

    [Fact]
    public void SaveAnswer_EmptyBody_IsValid()
    {
        // Every field empty means "empty the answer", so nothing is required.
        Assert.Empty(Validate(new SaveAnswerDto()));
    }

    [Fact]
    public void SaveAnswer_TextAndNoteAtTheirLimits_AreValid()
    {
        Assert.Empty(Validate(new SaveAnswerDto { Text = new string('a', 4000), Note = new string('a', 2000) }));
    }

    [Fact]
    public void SaveAnswer_TextTooLong_HasTurkishMessage()
    {
        var error = SingleErrorOn(new SaveAnswerDto { Text = new string('a', 4001) }, nameof(SaveAnswerDto.Text));

        Assert.Equal("Yanıt en fazla 4000 karakter olabilir.", error.ErrorMessage);
    }

    [Fact]
    public void SaveAnswer_NoteTooLong_HasTurkishMessage()
    {
        var error = SingleErrorOn(new SaveAnswerDto { Note = new string('a', 2001) }, nameof(SaveAnswerDto.Note));

        Assert.Equal("Not en fazla 2000 karakter olabilir.", error.ErrorMessage);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void SaveAnswer_ProbabilityAndSeverityOneToThree_AreValid(int value)
    {
        Assert.Empty(Validate(new SaveAnswerDto { OptionId = 1, Probability = value, Severity = value }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(5)]  // the old 1–5 risk scale is not accepted
    public void SaveAnswer_ProbabilityOutOfRange_HasTurkishMessage(int value)
    {
        var error = SingleErrorOn(new SaveAnswerDto { Probability = value }, nameof(SaveAnswerDto.Probability));

        Assert.Equal("Olasılık 1 ile 3 arasında olmalıdır.", error.ErrorMessage);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void SaveAnswer_SeverityOutOfRange_HasTurkishMessage(int value)
    {
        var error = SingleErrorOn(new SaveAnswerDto { Severity = value }, nameof(SaveAnswerDto.Severity));

        Assert.Equal("Şiddet 1 ile 3 arasında olmalıdır.", error.ErrorMessage);
    }

    // ---- SaveQuestionnaireDto ----

    [Fact]
    public void SaveQuestionnaire_SubcategoryOnly_IsValid()
    {
        Assert.Empty(Validate(new SaveQuestionnaireDto { Subcategory = RequestSource.Affiliated }));
    }

    [Fact]
    public void SaveQuestionnaire_NoSubcategory_HasTurkishMessage()
    {
        var error = SingleErrorOn(new SaveQuestionnaireDto(), nameof(SaveQuestionnaireDto.Subcategory));

        Assert.Equal("Alt kategori seçilmelidir.", error.ErrorMessage);
    }

    [Fact]
    public void SaveQuestionnaire_ParticipantsAtTheLimit_IsValid_OneMore_HasTurkishMessage()
    {
        Assert.Empty(Validate(new SaveQuestionnaireDto { Subcategory = RequestSource.Internal, Participants = new string('a', 1000) }));

        var error = SingleErrorOn(
            new SaveQuestionnaireDto { Subcategory = RequestSource.Internal, Participants = new string('a', 1001) },
            nameof(SaveQuestionnaireDto.Participants));
        Assert.Equal("Katılanlar en fazla 1000 karakter olabilir.", error.ErrorMessage);
    }

    // ---- QuestionFilter (query string) ----

    [Fact]
    public void QuestionFilter_NoSubcategory_HasTurkishMessage()
    {
        var error = SingleErrorOn(new QuestionFilter { WorkType = "new-software-project" }, nameof(QuestionFilter.Subcategory));

        Assert.Equal("Alt kategori seçilmelidir.", error.ErrorMessage);
    }

    [Fact]
    public void QuestionFilter_WorkTypeOver50Characters_IsInvalid()
    {
        SingleErrorOn(
            new QuestionFilter { Subcategory = RequestSource.Internal, WorkType = new string('a', 51) },
            nameof(QuestionFilter.WorkType));
    }
}
