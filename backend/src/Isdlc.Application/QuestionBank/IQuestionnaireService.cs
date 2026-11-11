using Isdlc.Application.QuestionBank.Dtos;

namespace Isdlc.Application.QuestionBank;

/// <summary>
/// The forms filled in on a project: one per project and set, created with the first save.
/// Read on any project, written only on an active one (409). Contract: docs/question-bank-api.md
/// </summary>
public interface IQuestionnaireService
{
    /// <summary>The form with its questions, answers and summary; empty answers when nothing was saved yet.</summary>
    Task<QuestionnaireDto> GetAsync(int projectId, string setCode, CancellationToken cancellationToken);

    /// <summary>Saves the header: subcategory, participants, date, approver.</summary>
    Task<QuestionnaireDto> SaveAsync(int projectId, string setCode, SaveQuestionnaireDto request, CancellationToken cancellationToken);

    /// <summary>Saves one answer; every field empty empties it. 404 when the question is not on this form.</summary>
    Task<QuestionnaireDto> SaveAnswerAsync(
        int projectId, string setCode, int questionId, SaveAnswerDto request, CancellationToken cancellationToken);
}
