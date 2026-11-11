using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.QuestionBank.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Isdlc.Application.QuestionBank;

public class QuestionBankService(IApplicationDbContext dbContext) : IQuestionBankService
{
    public async Task<IReadOnlyList<QuestionSetDto>> ListSetsAsync(CancellationToken cancellationToken) =>
        await dbContext.QuestionSets
            .AsNoTracking()
            .OrderBy(s => s.Id)
            .Select(s => new QuestionSetDto(s.Code, s.Name, s.SourceDocument, s.Version, s.AnswerMode))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<QuestionDto>> ListQuestionsAsync(
        string setCode, QuestionFilter filter, CancellationToken cancellationToken)
    {
        var set = await dbContext.GetSetAsync(setCode, cancellationToken);
        var workType = string.IsNullOrWhiteSpace(filter.WorkType) ? QuestionnaireRules.DefaultWorkType : filter.WorkType;

        // Subcategory is [Required]; the API answers 400 before this runs.
        return await dbContext.ListQuestionsAsync(set, workType, filter.Subcategory!.Value, cancellationToken);
    }
}
