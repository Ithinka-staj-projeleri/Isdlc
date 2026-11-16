using Isdlc.Application.Common;
using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.OpenIssues;
using Isdlc.Application.QuestionBank.Dtos;
using Isdlc.Domain.OpenIssues;
using Isdlc.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace Isdlc.Application.QuestionBank;

public class RequestAnalysisService(
    IApplicationDbContext dbContext,
    IQuestionnaireService questionnaires,
    ICurrentUserService currentUser) : IRequestAnalysisService
{
    /// <summary>The first try plus 3 retries when another record took the same code at the same moment.</summary>
    private const int MaxCodeAttempts = 4;

    public async Task<IReadOnlyList<RequestAnalysisOpenItemDto>> ListOpenItemsAsync(
        int projectId, CancellationToken cancellationToken)
    {
        // The form decides which questions are listed (work type, subcategory); its answers come with it.
        var form = await questionnaires.GetAsync(projectId, RequestAnalysisRules.SetCode, cancellationToken);
        var transferred = await TransferredAsync(projectId, cancellationToken);

        return form.Questions
            .Select(item => ToOpenItem(item, transferred))
            .OfType<RequestAnalysisOpenItemDto>()
            .ToList();
    }

    public async Task<RequestAnalysisOpenItemDto> TransferToOpenIssueAsync(
        int projectId, int questionId, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is null)
        {
            throw new ForbiddenException("İşlem yapmak için giriş yapılmalıdır.");
        }

        await dbContext.GetWritableProjectAsync(projectId, cancellationToken);
        var requester = await dbContext.Projects
            .Where(p => p.Id == projectId)
            .Select(p => p.RequesterName)
            .SingleAsync(cancellationToken);

        var form = await questionnaires.GetAsync(projectId, RequestAnalysisRules.SetCode, cancellationToken);
        var item = form.Questions.SingleOrDefault(i => i.Question.Id == questionId)
            ?? throw new NotFoundException("Soru bu formda yer almıyor.");
        var option = item.Question.Options.SingleOrDefault(o => o.Id == item.Answer?.OptionId);
        if (option is null || !RequestAnalysisRules.IsOpenItem(option.Meaning))
        {
            throw new BusinessRuleException(
                "Yalnızca \"Açık Konu Var\" ya da \"Kısmen Yanıtlandı\" cevabı verilen sorular Açık Konular'a aktarılır.");
        }

        var answerId = await dbContext.QuestionAnswers
            .Where(a => a.ProjectId == projectId
                && a.QuestionId == questionId
                && a.Questionnaire.QuestionSet.Code == RequestAnalysisRules.SetCode)
            .Select(a => a.Id)
            .SingleAsync(cancellationToken);

        await EnsureNotTransferredAsync(answerId, cancellationToken);

        // The customer's reply is awaited, so the record is the requester's until someone else takes it over.
        var issue = new OpenIssue
        {
            ProjectId = projectId,
            Type = OpenIssueType.Question,
            Title = RequestAnalysisRules.Title(item.Question.Text),
            Description = RequestAnalysisRules.Description(
                item.Question.No, item.Question.Group, option.Label, item.Answer!.Note, item.Answer.Text),
            OwnerName = requester,
            Status = OpenIssueStatus.Open,
            SourceQuestionAnswerId = answerId,
        };
        dbContext.OpenIssues.Add(issue);
        await SaveWithNewCodeAsync(issue, answerId, cancellationToken);

        return (await ListOpenItemsAsync(projectId, cancellationToken)).Single(i => i.QuestionId == questionId);
    }

    // ---- Helpers ----

    private static RequestAnalysisOpenItemDto? ToOpenItem(
        QuestionnaireItemDto item, IReadOnlyDictionary<int, TransferredOpenIssueDto> transferred)
    {
        var option = item.Question.Options.SingleOrDefault(o => o.Id == item.Answer?.OptionId);
        return option?.Meaning is { } meaning && RequestAnalysisRules.IsOpenItem(meaning)
            ? new RequestAnalysisOpenItemDto(
                item.Question.Id,
                item.Question.No,
                item.Question.Section,
                item.Question.Group,
                item.Question.Text,
                meaning,
                option.Label,
                item.Answer!.Text,
                item.Answer.Note,
                transferred.GetValueOrDefault(item.Question.Id))
            : null;
    }

    /// <summary>The project's transferred request analysis answers, by question.</summary>
    private async Task<IReadOnlyDictionary<int, TransferredOpenIssueDto>> TransferredAsync(
        int projectId, CancellationToken cancellationToken) =>
        await dbContext.OpenIssues
            .AsNoTracking()
            .Where(x => x.ProjectId == projectId
                && x.SourceQuestionAnswer != null
                && x.SourceQuestionAnswer.Questionnaire.QuestionSet.Code == RequestAnalysisRules.SetCode)
            .Select(x => new
            {
                x.SourceQuestionAnswer!.QuestionId,
                Issue = new TransferredOpenIssueDto(x.Id, x.Code, x.Status),
            })
            .ToDictionaryAsync(x => x.QuestionId, x => x.Issue, cancellationToken);

    private async Task EnsureNotTransferredAsync(int answerId, CancellationToken cancellationToken)
    {
        var code = await dbContext.OpenIssues
            .AsNoTracking()
            .Where(x => x.SourceQuestionAnswerId == answerId)
            .Select(x => x.Code)
            .FirstOrDefaultAsync(cancellationToken);

        if (code is not null)
        {
            throw new ConflictException($"Bu soru zaten Açık Konular'a aktarıldı ({code}).");
        }
    }

    /// <summary>
    /// Gives the record the next "AK-…" code of the project and saves it. Two records saved at the same moment can pick
    /// the same code; the unique index stops the second one, which then tries again with a new number. A second transfer
    /// of the same answer at the same moment is stopped by its own unique index (409).
    /// </summary>
    private async Task SaveWithNewCodeAsync(OpenIssue issue, int answerId, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            var number = await dbContext.OpenIssues
                .Where(x => x.ProjectId == issue.ProjectId)
                .Select(x => x.Code)
                .NextNumberAsync(cancellationToken);
            issue.Code = OpenIssueRules.FormatCode(OpenIssueRules.CodePrefix, number);

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateException)
            {
                await EnsureNotTransferredAsync(answerId, cancellationToken);

                var codeTaken = await dbContext.OpenIssues.AsNoTracking()
                    .AnyAsync(x => x.ProjectId == issue.ProjectId && x.Code == issue.Code, cancellationToken);
                if (!codeTaken)
                {
                    throw; // Not a code clash: let the real error through.
                }
                if (attempt == MaxCodeAttempts)
                {
                    throw new ConflictException("Açık konu bu arada başka biri tarafından kaydedildi. Sayfayı yenileyin.");
                }
            }
        }
    }
}
