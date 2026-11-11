using Isdlc.Application.QuestionBank.Dtos;

namespace Isdlc.Application.QuestionBank;

/// <summary>The forms and their questions (reference data, read only). Contract: docs/question-bank-api.md</summary>
public interface IQuestionBankService
{
    Task<IReadOnlyList<QuestionSetDto>> ListSetsAsync(CancellationToken cancellationToken);

    /// <summary>The questions of a work type and subcategory, before any form is filled in. 404 for an unknown set.</summary>
    Task<IReadOnlyList<QuestionDto>> ListQuestionsAsync(string setCode, QuestionFilter filter, CancellationToken cancellationToken);
}
