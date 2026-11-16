using Isdlc.Domain.OpenIssues;
using Isdlc.Domain.QuestionBank;

namespace Isdlc.Application.QuestionBank.Dtos;

/// <summary>The open issue an answer was transferred to.</summary>
public record TransferredOpenIssueDto(int Id, string Code, OpenIssueStatus Status);

/// <summary>
/// A Talep Analizi question whose answer still needs something ("Açık Konu Var" or "Kısmen Yanıtlandı"),
/// GET /projects/{projectId}/request-analysis/open-items. Contract: docs/question-bank-api.md → "Talep Analizi (E3)".
/// </summary>
public record RequestAnalysisOpenItemDto(
    int QuestionId,
    int No,
    string? Section,
    string? Group,
    string Text,
    OptionMeaning Meaning,
    string OptionLabel,
    string? AnswerText,               // the stakeholder's answer
    string? Note,                     // the open issue / missing information
    TransferredOpenIssueDto? OpenIssue); // null until it is transferred to Açık Konular
