using Isdlc.Application.QuestionBank.Dtos;

namespace Isdlc.Application.QuestionBank;

/// <summary>
/// The Talep Analizi tab's own part (E3): the questions still open and their transfer to Açık Konular.
/// The form itself is read and filled in with IQuestionnaireService ("request-analysis").
/// Contract: docs/question-bank-api.md → "Talep Analizi (E3)".
/// </summary>
public interface IRequestAnalysisService
{
    /// <summary>
    /// Listed questions answered "Açık Konu Var" or "Kısmen Yanıtlandı", in form order, with the open issue each was
    /// transferred to. 404 when the project does not exist.
    /// </summary>
    Task<IReadOnlyList<RequestAnalysisOpenItemDto>> ListOpenItemsAsync(int projectId, CancellationToken cancellationToken);

    /// <summary>
    /// Opens a question ("Soru") record in Açık Konular for the answer, owned by the person who passed the request on
    /// (the customer's reply is awaited). 404 unknown project or question, 409 project not active or already
    /// transferred, 400 when the answer is not open.
    /// </summary>
    Task<RequestAnalysisOpenItemDto> TransferToOpenIssueAsync(
        int projectId, int questionId, CancellationToken cancellationToken);
}
