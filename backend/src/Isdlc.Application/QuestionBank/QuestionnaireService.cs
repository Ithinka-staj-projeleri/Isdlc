using Isdlc.Application.Common;
using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Common.Models;
using Isdlc.Application.QuestionBank.Dtos;
using Isdlc.Domain.Projects;
using Isdlc.Domain.QuestionBank;
using Microsoft.EntityFrameworkCore;

namespace Isdlc.Application.QuestionBank;

public class QuestionnaireService(IApplicationDbContext dbContext, ICurrentUserService currentUser) : IQuestionnaireService
{
    public async Task<QuestionnaireDto> GetAsync(int projectId, string setCode, CancellationToken cancellationToken)
    {
        var source = await GetProjectSourceAsync(projectId, cancellationToken);
        var set = await dbContext.GetSetAsync(setCode, cancellationToken);

        var form = await dbContext.Questionnaires
            .AsNoTracking()
            .Where(x => x.ProjectId == projectId && x.QuestionSetId == set.Id)
            .Select(x => new
            {
                x.Id, x.WorkType, x.Subcategory, x.Participants, x.AssessedOn, x.ApprovedOn,
                ApprovedBy = x.ApprovedBy == null ? null : new UserSummaryDto(x.ApprovedBy.Id, x.ApprovedBy.FullName),
            })
            .SingleOrDefaultAsync(cancellationToken);

        // Before the first save the form follows the project: its request source is the subcategory.
        var workType = form?.WorkType ?? QuestionnaireRules.DefaultWorkType;
        var subcategory = form?.Subcategory ?? source;
        var questions = await dbContext.ListQuestionsAsync(set, workType, subcategory, cancellationToken);

        var answers = form is null
            ? []
            : await dbContext.QuestionAnswers
                .AsNoTracking()
                .Where(a => a.QuestionnaireId == form.Id)
                .Select(a => new
                {
                    a.QuestionId,
                    Answer = new AnswerDto(
                        a.OptionId, a.Text, a.Note, a.Probability, a.Severity,
                        dbContext.Users
                            .Where(u => u.Id == (a.UpdatedById ?? a.CreatedById))
                            .Select(u => new UserSummaryDto(u.Id, u.FullName))
                            .FirstOrDefault(),
                        a.UpdatedAt ?? a.CreatedAt),
                })
                .ToDictionaryAsync(a => a.QuestionId, a => a.Answer, cancellationToken);

        // Answers of questions no longer listed (subcategory changed, question left the file) stay but are not shown.
        var items = questions
            .Select(q => new QuestionnaireItemDto(q, answers.GetValueOrDefault(q.Id)))
            .ToList();

        return new QuestionnaireDto(
            projectId,
            set.Dto,
            workType,
            subcategory,
            form?.Participants,
            form?.AssessedOn,
            form?.ApprovedBy,
            form?.ApprovedOn,
            items,
            QuestionnaireRules.Summarize(items),
            Result: null);
    }

    public async Task<QuestionnaireDto> SaveAsync(
        int projectId, string setCode, SaveQuestionnaireDto request, CancellationToken cancellationToken)
    {
        RequireSignedIn();
        await dbContext.GetWritableProjectAsync(projectId, cancellationToken);
        var set = await dbContext.GetSetAsync(setCode, cancellationToken);

        if (request.ApprovedByUserId is { } approverId
            && !await dbContext.Users.AnyAsync(u => u.Id == approverId && u.IsActive, cancellationToken))
        {
            throw new BusinessRuleException("Seçilen onaylayan aktif bir kullanıcı değil.");
        }

        var form = await LoadOrAddAsync(projectId, set, cancellationToken);
        form.Subcategory = request.Subcategory!.Value; // [Required]: the API answers 400 before this runs.
        form.Participants = QuestionnaireRules.Clean(request.Participants);
        form.AssessedOn = request.AssessedOn;
        form.ApprovedById = request.ApprovedByUserId;
        form.ApprovedOn = request.ApprovedOn;

        await SaveChangesAsync(cancellationToken, form);
        return await GetAsync(projectId, setCode, cancellationToken);
    }

    public async Task<QuestionnaireDto> SaveAnswerAsync(
        int projectId, string setCode, int questionId, SaveAnswerDto request, CancellationToken cancellationToken)
    {
        RequireSignedIn();
        await dbContext.GetWritableProjectAsync(projectId, cancellationToken);
        var set = await dbContext.GetSetAsync(setCode, cancellationToken);
        var form = await LoadOrAddAsync(projectId, set, cancellationToken);

        var question = (await dbContext.ListQuestionsAsync(set, form.WorkType, form.Subcategory, cancellationToken, questionId))
            .SingleOrDefault()
            ?? throw new NotFoundException("Soru bu formda yer almıyor.");

        QuestionnaireRules.Validate(question, request);

        var answer = form.Id == 0
            ? null
            : await dbContext.QuestionAnswers.SingleOrDefaultAsync(
                a => a.QuestionnaireId == form.Id && a.QuestionId == questionId, cancellationToken);

        if (QuestionnaireRules.IsEmpty(request))
        {
            // Emptied, never deleted: the answer's history stays in the audit log.
            if (answer is not null)
            {
                answer.OptionId = null;
                answer.Text = null;
                answer.Note = null;
                answer.Probability = null;
                answer.Severity = null;
                await SaveChangesAsync(cancellationToken, answer);
            }

            // Nothing was saved before: an empty answer does not create the form.
            return await GetAsync(projectId, setCode, cancellationToken);
        }

        if (answer is null)
        {
            answer = new QuestionAnswer { Questionnaire = form, ProjectId = projectId, QuestionId = questionId };
            dbContext.QuestionAnswers.Add(answer);
        }

        answer.OptionId = request.OptionId;
        answer.Text = QuestionnaireRules.Clean(request.Text);
        answer.Note = QuestionnaireRules.Clean(request.Note);
        answer.Probability = request.Probability;
        answer.Severity = request.Severity;

        await SaveChangesAsync(cancellationToken, form, answer);
        return await GetAsync(projectId, setCode, cancellationToken);
    }

    // ---- Helpers ----

    private void RequireSignedIn()
    {
        if (currentUser.UserId is null)
        {
            throw new ForbiddenException("İşlem yapmak için giriş yapılmalıdır.");
        }
    }

    private async Task<RequestSource> GetProjectSourceAsync(int projectId, CancellationToken cancellationToken)
    {
        var project = await dbContext.Projects
            .AsNoTracking()
            .Where(p => p.Id == projectId)
            .Select(p => new { p.Source })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Proje bulunamadı.");

        return project.Source;
    }

    /// <summary>The project's form for the set; a new one (not yet added) taking the project's request source.</summary>
    private async Task<Questionnaire> LoadOrAddAsync(int projectId, QuestionSetRef set, CancellationToken cancellationToken)
    {
        var form = await dbContext.Questionnaires
            .SingleOrDefaultAsync(x => x.ProjectId == projectId && x.QuestionSetId == set.Id, cancellationToken);

        if (form is null)
        {
            form = new Questionnaire
            {
                ProjectId = projectId,
                QuestionSetId = set.Id,
                WorkType = QuestionnaireRules.DefaultWorkType,
                Subcategory = await GetProjectSourceAsync(projectId, cancellationToken),
            };
            dbContext.Questionnaires.Add(form);
        }

        return form;
    }

    /// <summary>
    /// If two people create the same form or the same answer at the same moment, the unique index stops the second;
    /// they are asked to reload instead of silently losing the other person's answer.
    /// </summary>
    private async Task SaveChangesAsync(CancellationToken cancellationToken, params object[] saved)
    {
        var addsRows = saved.Any(e => dbContext.Entry(e).State == EntityState.Added);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (addsRows && ex is not DbUpdateConcurrencyException)
        {
            throw new ConflictException("Form bu arada başka biri tarafından kaydedildi. Sayfayı yenileyin.");
        }
    }
}
