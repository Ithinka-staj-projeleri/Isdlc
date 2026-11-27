using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Common.Models;
using Isdlc.Application.Process;
using Isdlc.Application.QuestionBank;
using Isdlc.Application.Process.Dtos;
using Isdlc.Application.ProcessImprovements;
using Isdlc.Application.ProcessImprovements.Dtos;
using Isdlc.Application.Tests.Common;
using Isdlc.Domain.Process;
using Isdlc.Domain.ProcessImprovements;
using Isdlc.Domain.Projects;
using Isdlc.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Isdlc.Application.Tests.Tenants;

/// <summary>
/// The process and process improvement proposals across companies (Faz 6 · T7): each company works on its own copy of the
/// flows, cannot see or move another company's record, cannot jump to another company's step, and numbers its proposals
/// on its own. Expectations: docs/multi-tenancy.md → Testler. Cross test (PLANNING.md): Dide → Erva's modules.
/// </summary>
[Collection("Database")]
public class ProcessIsolationTests(DatabaseFixture database)
{
    private async Task<T> ProcessAsync<T>(int tenantId, int userId, Func<ProcessService, Task<T>> action)
    {
        await using var scope = database.CreateScope(tenantId);
        var service = new ProcessService(
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(), new TestUser(userId, tenantId),
            new RecordingNotificationService(), TimeProvider.System,
            scope.ServiceProvider.GetRequiredService<IQuestionnaireService>());
        return await action(service);
    }

    private async Task<T> ImprovementsAsync<T>(int tenantId, int userId, Func<ProcessImprovementService, Task<T>> action)
    {
        await using var scope = database.CreateScope(tenantId);
        var service = new ProcessImprovementService(
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(), new TestUser(userId, tenantId),
            new RecordingNotificationService(), TimeProvider.System);
        return await action(service);
    }

    private Task<int> IthinkaUserAsync(string email) => TestProjects.UserIdAsync(database, email);

    private Task<int> DenemeUserAsync(string email) => TestProjects.UserIdAsync(database, email, database.DenemeId);

    private static CreateImprovementDto NewProposal(int ownerUserId) => new()
    {
        Title = "Talep kaydında mükerrer alanlar",
        Problem = "Müşteri bilgisi iki kez giriliyor.",
        Source = ImprovementSource.EmployeeSuggestion,
        AffectedProcess = "Talep kaydı",
        SuggestedBy = "Deniz Analist",
        OwnerUserId = ownerUserId,
    };

    // ---- Flows ----

    [Fact]
    public async Task Flows_EachCompanyGetsItsOwnCopy()
    {
        var ithinkaId = await IthinkaUserAsync(TestProjects.Analyst);
        var denemeId = await DenemeUserAsync(TestTenants.DenemeAnalyst);

        var ithinka = await ProcessAsync(Tenant.IthinkaId, ithinkaId, s => s.GetFlowsAsync(CancellationToken.None));
        var deneme = await ProcessAsync(database.DenemeId, denemeId, s => s.GetFlowsAsync(CancellationToken.None));

        // Same templates, so the same codes; different rows, so no shared id.
        Assert.Contains(deneme, f => f.Code == ProcessFlowCodes.NewRequest);
        Assert.Empty(deneme.Select(f => f.Id).Intersect(ithinka.Select(f => f.Id)));

        var denemeFlow = await ProcessAsync(database.DenemeId, denemeId,
            s => s.GetFlowAsync(ProcessFlowCodes.NewRequest, false, CancellationToken.None));
        Assert.Equal(deneme.Single(f => f.Code == ProcessFlowCodes.NewRequest).Id, denemeFlow.Id);
    }

    // ---- A project's process ----

    [Fact]
    public async Task Process_OtherCompany_CannotReadTheProcessOrItsHistory()
    {
        var denemeId = await DenemeUserAsync(TestTenants.DenemeAnalyst);
        var projectId = await TestProjects.CreateAsync(database, "4");

        await Assert.ThrowsAsync<NotFoundException>(() => ProcessAsync(database.DenemeId, denemeId,
            s => s.GetProjectProcessAsync(projectId, CancellationToken.None)));
        await Assert.ThrowsAsync<NotFoundException>(() => ProcessAsync(database.DenemeId, denemeId,
            s => s.GetStepRecordsAsync(projectId, new PageRequest(), CancellationToken.None)));
        await Assert.ThrowsAsync<NotFoundException>(() => ProcessAsync(database.DenemeId, denemeId,
            s => s.GetRequestDecisionsAsync(projectId, CancellationToken.None)));
    }

    [Fact]
    public async Task Process_OtherCompany_CannotMoveTheProject()
    {
        // Deneme's analyst holds the role step 1 needs; only the company is wrong.
        var denemeId = await DenemeUserAsync(TestTenants.DenemeAnalyst);
        var projectId = await TestProjects.CreateAsync(database, "1");
        var stepId = await TestProjects.StepIdAsync(database, "1");

        await Assert.ThrowsAsync<NotFoundException>(() => ProcessAsync(database.DenemeId, denemeId,
            s => s.CompleteStepAsync(projectId, new CompleteStepDto { ExpectedStepId = stepId }, CancellationToken.None)));

        var project = await TestProjects.GetAsync(database, projectId);
        Assert.Equal(stepId, project.CurrentStepId);
    }

    [Fact]
    public async Task Process_AnotherCompanysStep_IsNotAJumpTarget()
    {
        // Step 10 "No" may go back to 9; Ithinka's step 9 is not one of Deneme's flow.
        var denemeManagerId = await DenemeUserAsync(TestTenants.DenemeManager);
        var denemeProjectId = await TestProjects.CreateAsync(
            database, "10", source: RequestSource.External, tenantId: database.DenemeId);
        var denemeStep10 = await TestProjects.StepIdAsync(database, "10", tenantId: database.DenemeId);
        var ithinkaStep9 = await TestProjects.StepIdAsync(database, "9");

        await Assert.ThrowsAsync<BusinessRuleException>(() => ProcessAsync(database.DenemeId, denemeManagerId,
            s => s.AnswerStepAsync(denemeProjectId,
                new AnswerStepDto { ExpectedStepId = denemeStep10, Answer = StepAnswer.No, JumpTargetStepId = ithinkaStep9 },
                CancellationToken.None)));

        // Read as Deneme: an Ithinka scope (TestProjects.GetAsync) cannot see Deneme's project.
        var process = await ProcessAsync(database.DenemeId, denemeManagerId,
            s => s.GetProjectProcessAsync(denemeProjectId, CancellationToken.None));
        Assert.Equal(denemeStep10, process.CurrentStep!.Id);
    }

    [Fact]
    public async Task Process_OwnCompanysStep_IsAJumpTarget()
    {
        // The same answer with Deneme's own step 9 works: the refusal above is about the company, not the jump.
        var denemeManagerId = await DenemeUserAsync(TestTenants.DenemeManager);
        var denemeProjectId = await TestProjects.CreateAsync(
            database, "10", source: RequestSource.External, tenantId: database.DenemeId);
        var denemeStep10 = await TestProjects.StepIdAsync(database, "10", tenantId: database.DenemeId);
        var denemeStep9 = await TestProjects.StepIdAsync(database, "9", tenantId: database.DenemeId);

        var result = await ProcessAsync(database.DenemeId, denemeManagerId,
            s => s.AnswerStepAsync(denemeProjectId,
                new AnswerStepDto { ExpectedStepId = denemeStep10, Answer = StepAnswer.No, JumpTargetStepId = denemeStep9 },
                CancellationToken.None));

        Assert.Equal("9", result.CurrentStep!.Code);
    }

    // ---- Process improvement proposals ----

    [Fact]
    public async Task Improvement_OtherCompany_CannotListReadOrChangeIt()
    {
        var ithinkaId = await IthinkaUserAsync(TestProjects.Analyst);
        var denemeId = await DenemeUserAsync(TestTenants.DenemeAnalyst);
        var proposal = await ImprovementsAsync(Tenant.IthinkaId, ithinkaId,
            s => s.CreateAsync(NewProposal(ithinkaId), CancellationToken.None));

        var list = await ImprovementsAsync(database.DenemeId, denemeId,
            s => s.ListAsync(new ImprovementFilter(), new PageRequest { Page = 1, PageSize = 100 }, CancellationToken.None));
        Assert.DoesNotContain(list.Items, i => i.Id == proposal.Id);

        await Assert.ThrowsAsync<NotFoundException>(() => ImprovementsAsync(database.DenemeId, denemeId,
            s => s.GetAsync(proposal.Id, CancellationToken.None)));
        await Assert.ThrowsAsync<NotFoundException>(() => ImprovementsAsync(database.DenemeId, denemeId,
            s => s.SavePrioritizationAsync(proposal.Id,
                new SavePrioritizationDto { Impact = 1, Frequency = 1, Ease = 1 }, CancellationToken.None)));
    }

    [Fact]
    public async Task Improvement_OtherCompanysUser_CannotBeTheOwner()
    {
        var ithinkaId = await IthinkaUserAsync(TestProjects.Analyst);
        var denemeId = await DenemeUserAsync(TestTenants.DenemeAnalyst);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => ImprovementsAsync(database.DenemeId, denemeId,
            s => s.CreateAsync(NewProposal(ithinkaId), CancellationToken.None)));

        Assert.Equal("Seçilen kişi aktif bir kullanıcı değil.", error.Message);
    }

    [Fact]
    public async Task Improvement_Codes_FollowOnlyTheCompanysOwnProposals()
    {
        // Ithinka has many SI- codes from other tests; Deneme's next code follows only Deneme's.
        var ithinkaId = await IthinkaUserAsync(TestProjects.Analyst);
        var denemeId = await DenemeUserAsync(TestTenants.DenemeAnalyst);
        await ImprovementsAsync(Tenant.IthinkaId, ithinkaId, s => s.CreateAsync(NewProposal(ithinkaId), CancellationToken.None));
        var denemeBefore = await HighestImprovementNumberAsync(database.DenemeId);

        var proposal = await ImprovementsAsync(database.DenemeId, denemeId,
            s => s.CreateAsync(NewProposal(denemeId), CancellationToken.None));

        Assert.Equal($"SI-{denemeBefore + 1:D3}", proposal.Code);
    }

    private async Task<int> HighestImprovementNumberAsync(int tenantId)
    {
        await using var scope = database.CreateScope(tenantId);
        var codes = await scope.ServiceProvider.GetRequiredService<IApplicationDbContext>().Projects
            .Where(p => p.ProcessFlow.Code == ProcessFlowCodes.ProcessImprovement)
            .Select(p => p.Code)
            .ToListAsync();
        return codes.Select(c => int.Parse(c["SI-".Length..])).DefaultIfEmpty(0).Max();
    }
}
