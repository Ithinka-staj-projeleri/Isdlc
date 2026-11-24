using Isdlc.Application.Assessment;
using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.QuestionBank;
using Isdlc.Application.QuestionBank.Dtos;
using Isdlc.Application.Tests.Common;
using Isdlc.Domain.Projects;
using Isdlc.Domain.Tenants;
using Microsoft.Extensions.DependencyInjection;

namespace Isdlc.Application.Tests.Tenants;

/// <summary>
/// The question bank, the forms on a project and the level across companies (Faz 6 · T7): each company answers its own
/// copy of the questions, and nobody opens another company's project form. Expectations: docs/multi-tenancy.md → Testler.
/// Cross test (PLANNING.md): Erva → Baran's modules.
/// </summary>
[Collection("Database")]
public class QuestionBankIsolationTests(DatabaseFixture database)
{
    private async Task<T> AsAsync<T>(int tenantId, int userId, Func<IApplicationDbContext, ICurrentUserService, Task<T>> action)
    {
        await using var scope = database.CreateScope(tenantId);
        return await action(scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(), new TestUser(userId, tenantId));
    }

    private Task<int> DenemeAnalystAsync() => TestProjects.UserIdAsync(database, TestTenants.DenemeAnalyst, database.DenemeId);

    [Fact]
    public async Task Questions_EachCompanyListsItsOwnCopy()
    {
        var ithinkaAnalystId = await TestProjects.UserIdAsync(database, TestProjects.Analyst);
        var denemeAnalystId = await DenemeAnalystAsync();

        var ithinka = await AsAsync(Tenant.IthinkaId, ithinkaAnalystId,
            (db, _) => new QuestionBankService(db).ListQuestionsAsync("level", new QuestionFilter { Subcategory = RequestSource.External }, CancellationToken.None));
        var deneme = await AsAsync(database.DenemeId, denemeAnalystId,
            (db, _) => new QuestionBankService(db).ListQuestionsAsync("level", new QuestionFilter { Subcategory = RequestSource.External }, CancellationToken.None));

        // The same questions from the same template, but each company's own rows.
        Assert.Equal(ithinka.Select(q => q.Code), deneme.Select(q => q.Code));
        Assert.Empty(ithinka.Select(q => q.Id).Intersect(deneme.Select(q => q.Id)));
    }

    [Fact]
    public async Task ProjectForm_OtherCompanysProject_IsNotFound()
    {
        var projectId = await TestProjects.CreateAsync(database, "5");
        var denemeAnalystId = await DenemeAnalystAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => AsAsync(database.DenemeId, denemeAnalystId,
            (db, user) => new QuestionnaireService(db, user).GetAsync(projectId, "level", CancellationToken.None)));
    }

    [Fact]
    public async Task RequestAnalysis_OtherCompanysProject_IsNotFound()
    {
        var projectId = await TestProjects.CreateAsync(database, "5");
        var denemeAnalystId = await DenemeAnalystAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => AsAsync(database.DenemeId, denemeAnalystId, (db, user) =>
        {
            var questionnaires = new QuestionnaireService(db, user);
            return new RequestAnalysisService(db, questionnaires, user).ListOpenItemsAsync(projectId, CancellationToken.None);
        }));
    }

    [Fact]
    public async Task ApplyLevel_OtherCompanysProject_IsNotFound()
    {
        var projectId = await TestProjects.CreateAsync(database, "5");
        var denemeAnalystId = await DenemeAnalystAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => AsAsync(database.DenemeId, denemeAnalystId,
            (db, user) => new AssessmentService(db, user, new QuestionnaireService(db, user)).ApplyLevelAsync(projectId, CancellationToken.None)));
    }

    [Fact]
    public async Task ProjectForm_OwnProject_UsesTheCompanysOwnQuestionSet()
    {
        // Deneme opens the level form of its own project; the form is built from Deneme's copy of the set.
        var denemeAnalystId = await DenemeAnalystAsync();
        var projectId = await TestProjects.CreateAsync(database, "5", tenantId: database.DenemeId);

        var form = await AsAsync(database.DenemeId, denemeAnalystId,
            (db, user) => new QuestionnaireService(db, user).GetAsync(projectId, "level", CancellationToken.None));
        var denemeQuestionIds = (await AsAsync(database.DenemeId, denemeAnalystId,
            (db, _) => new QuestionBankService(db).ListQuestionsAsync("level", new QuestionFilter { Subcategory = RequestSource.External }, CancellationToken.None)))
            .Select(q => q.Id).ToHashSet();

        Assert.Equal(projectId, form.ProjectId);
        Assert.Equal("level", form.Set.Code);
        Assert.NotEmpty(denemeQuestionIds);
    }
}
