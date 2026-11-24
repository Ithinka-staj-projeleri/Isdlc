using Isdlc.Application.Assessment.Dtos;
using Isdlc.Application.Common;
using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.QuestionBank;
using Isdlc.Application.QuestionBank.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Isdlc.Application.Assessment;

public class AssessmentService(
    IApplicationDbContext dbContext, ICurrentUserService currentUser, IQuestionnaireService questionnaires) : IAssessmentService
{
    public async Task<QuestionnaireDto> ApplyLevelAsync(int projectId, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is null)
        {
            throw new ForbiddenException("İşlem yapmak için giriş yapılmalıdır.");
        }

        await dbContext.GetWritableProjectAsync(projectId, cancellationToken);

        var form = await questionnaires.GetAsync(projectId, LevelRules.SetCode, cancellationToken);
        if (form.Result is not LevelResultDto result || !LevelRules.CanApply(result))
        {
            throw new BusinessRuleException("Seviyeyi uygulamak için bütün sorular cevaplanmalıdır.");
        }

        var project = await dbContext.Projects.SingleAsync(p => p.Id == projectId, cancellationToken);
        project.Level = result.DocumentLevel.Level;
        project.ResearchDays = result.Research.Days;

        // A save at the same moment as another change to this project fails on RowVersion and returns 409.
        await dbContext.SaveChangesAsync(cancellationToken);
        return await questionnaires.GetAsync(projectId, LevelRules.SetCode, cancellationToken);
    }
}
