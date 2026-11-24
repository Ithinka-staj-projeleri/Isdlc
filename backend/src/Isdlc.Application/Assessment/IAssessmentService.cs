using Isdlc.Application.QuestionBank.Dtos;

namespace Isdlc.Application.Assessment;

/// <summary>
/// Seviye &amp; Uygunluk (Faz 4 · B2). The two forms are read and filled in with IQuestionnaireService ("level",
/// "customer-fit"), which also fills their result; this service only applies the level form's result to the project.
/// Contract: docs/assessment-api.md
/// </summary>
public interface IAssessmentService
{
    /// <summary>
    /// Writes the level form's result to Project.Level and Project.ResearchDays and returns the level form.
    /// 400 until both sections are answered; 404 project missing; 409 project not active or changed at the same moment.
    /// </summary>
    Task<QuestionnaireDto> ApplyLevelAsync(int projectId, CancellationToken cancellationToken);
}
