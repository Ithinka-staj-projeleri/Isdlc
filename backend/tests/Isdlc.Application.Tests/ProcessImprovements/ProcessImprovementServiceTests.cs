using System.ComponentModel.DataAnnotations;
using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Common.Models;
using Isdlc.Application.ProcessImprovements;
using Isdlc.Application.ProcessImprovements.Dtos;
using Isdlc.Application.Tests.Common;
using Isdlc.Application.Tests.Identity;
using Isdlc.Domain.Process;
using Isdlc.Domain.ProcessImprovements;
using Isdlc.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Isdlc.Application.Tests.ProcessImprovements;

/// <summary>
/// Process improvement proposals on a real database. Every test creates its own proposals; codes are shared by all
/// proposals, so tests compare codes with each other instead of expecting "SI-001". Contract: docs/process-improvement-api.md
/// </summary>
[Collection("Database")]
public class ProcessImprovementServiceTests(DatabaseFixture database)
{
    private async Task<(T Result, RecordingNotificationService Notifications)> AsAsync<T>(
        string? email, Func<IProcessImprovementService, Task<T>> action)
    {
        var userId = email is null ? (int?)null : await TestProjects.UserIdAsync(database, email);
        await using var scope = database.CreateScope();
        var notifications = new RecordingNotificationService();
        var service = new ProcessImprovementService(
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(), new TestUser(userId), notifications,
            TimeProvider.System);
        return (await action(service), notifications);
    }

    private async Task<T> AsAnalystAsync<T>(Func<IProcessImprovementService, Task<T>> action) =>
        (await AsAsync(TestProjects.Analyst, action)).Result;

    private async Task<CreateImprovementDto> NewProposalAsync(string? owner = TestProjects.Analyst) => new()
    {
        Title = "  Talep kaydında mükerrer alanlar  ",
        Problem = " Talep formunda müşteri bilgisi iki kez giriliyor. ",
        Source = ImprovementSource.EmployeeSuggestion,
        AffectedProcess = " Talep kaydı ",
        ProposedSolution = "   ",
        SuggestedBy = " Can Öztürk ",
        OwnerUserId = owner is null ? null : await TestProjects.UserIdAsync(database, owner),
    };

    private async Task<ImprovementDto> CreateAsync(CreateImprovementDto? request = null) =>
        await AsAnalystAsync(async s => await s.CreateAsync(request ?? await NewProposalAsync(), default));

    private static UpdateImprovementDto AsUpdate(CreateImprovementDto create) => new()
    {
        Title = create.Title, Problem = create.Problem, Source = create.Source, AffectedProcess = create.AffectedProcess,
        ProposedSolution = create.ProposedSolution, SuggestedBy = create.SuggestedBy, OwnerUserId = create.OwnerUserId,
    };

    private async Task SetStatusAsync(int id, ProjectStatus status)
    {
        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var project = await db.Projects.SingleAsync(p => p.Id == id);
        project.Status = status;
        project.CurrentStepId = null;
        await db.SaveChangesAsync();
    }

    private static int NumberOf(string code) => int.Parse(code["SI-".Length..]);

    // ---- Create ----

    [Fact]
    public async Task Create_StartsAtTheFlowsFirstStep_WithTrimmedFields()
    {
        var proposal = await CreateAsync();

        Assert.Matches(@"^SI-\d{3,}$", proposal.Code);
        Assert.Equal(ProjectStatus.Active, proposal.Status);
        Assert.Equal(("1", 1, "Planla"), (proposal.CurrentStep!.Code, proposal.CurrentStep.StageOrder, proposal.CurrentStep.StageShortTitle));
        Assert.Equal("Talep kaydında mükerrer alanlar", proposal.Title);
        Assert.Equal("Talep formunda müşteri bilgisi iki kez giriliyor.", proposal.Problem);
        Assert.Equal("Talep kaydı", proposal.AffectedProcess);
        Assert.Equal("Can Öztürk", proposal.SuggestedBy);
        Assert.Null(proposal.ProposedSolution);
        Assert.Equal("Elif Yılmaz", proposal.Owner!.FullName);
        Assert.Equal("Elif Yılmaz", proposal.CreatedBy.FullName);
        Assert.Null(proposal.Prioritization.Score);
        Assert.Null(proposal.Study.Solution);
        Assert.Null(proposal.PilotEvaluation.PilotResult);
    }

    [Fact]
    public async Task Create_IsAProjectOnTheImprovementFlow_NotARequest()
    {
        var proposal = await CreateAsync();

        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var flow = await db.Projects.Where(p => p.Id == proposal.Id).Select(p => p.ProcessFlow).SingleAsync();
        Assert.Equal(ProcessFlowCodes.ProcessImprovement, flow.Code);
        Assert.Equal(ProcessFlowKind.Improvement, flow.Kind);
    }

    [Fact]
    public async Task Create_SecondProposal_GetsTheNextCode()
    {
        var first = await CreateAsync();
        var second = await CreateAsync();

        Assert.Equal(NumberOf(first.Code) + 1, NumberOf(second.Code));
    }

    [Fact]
    public async Task Create_ForAnotherOwner_NotifiesThem_NotForOneself()
    {
        var forDeveloper = await AsAsync(TestProjects.Analyst,
            async s => await s.CreateAsync(await NewProposalAsync(TestProjects.Developer), default));
        var forSelf = await AsAsync(TestProjects.Analyst, async s => await s.CreateAsync(await NewProposalAsync(), default));

        var sent = Assert.Single(forDeveloper.Notifications.Sent);
        Assert.Equal([await TestProjects.UserIdAsync(database, TestProjects.Developer)], sent.UserIds);
        Assert.Equal(forDeveloper.Result.Id, sent.ProjectId);
        Assert.StartsWith($"{forDeveloper.Result.Code} süreç iyileştirme önerisi size atandı", sent.Message);
        // The notification service drops one's own notifications; the call itself is still made with the owner.
        Assert.Single(forSelf.Notifications.Sent);
    }

    [Fact]
    public async Task Create_InactiveOrUnknownOwner_Throws400_AndSavesNothing()
    {
        var inactive = await TestUsers.CreateAsync(database, password: null, isActive: false);

        foreach (var ownerId in new[] { inactive.Id, int.MaxValue })
        {
            var request = await NewProposalAsync() with { OwnerUserId = ownerId, Title = $"Pasif sahip {ownerId}" };
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateAsync(request));
            Assert.Equal("Seçilen kişi aktif bir kullanıcı değil.", ex.Message);
        }

        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        Assert.False(await db.Projects.AnyAsync(p => p.Name.StartsWith("Pasif sahip")));
    }

    [Fact]
    public async Task Create_NotSignedIn_Throws403()
    {
        var request = await NewProposalAsync();

        await Assert.ThrowsAsync<ForbiddenException>(() => AsAsync(null, s => s.CreateAsync(request, default)));
    }

    // ---- Read ----

    [Fact]
    public async Task Get_RequestProject_IsNotAProposal_404()
    {
        var requestId = await TestProjects.CreateAsync(database, "5");

        var ex = await Assert.ThrowsAsync<NotFoundException>(() => AsAnalystAsync(s => s.GetAsync(requestId, default)));
        Assert.Equal("Süreç iyileştirme önerisi bulunamadı.", ex.Message);
    }

    [Fact]
    public async Task List_FiltersByStatusAndStage_AndShowsPriority()
    {
        var active = await CreateAsync();
        var rejected = await CreateAsync();
        await SetStatusAsync(rejected.Id, ProjectStatus.Rejected);
        await AsAnalystAsync(s => s.SavePrioritizationAsync(active.Id,
            new SavePrioritizationDto { Impact = 5, Frequency = 4, Ease = 4 }, default));

        var activeAtPlan = await AsAnalystAsync(s => s.ListAsync(
            new ImprovementFilter { Status = ProjectStatus.Active, StageOrder = 1 }, new PageRequest { PageSize = 100 }, default));
        var rejectedOnes = await AsAnalystAsync(s => s.ListAsync(
            new ImprovementFilter { Status = ProjectStatus.Rejected }, new PageRequest { PageSize = 100 }, default));
        var atPilot = await AsAnalystAsync(s => s.ListAsync(
            new ImprovementFilter { StageOrder = 2 }, new PageRequest { PageSize = 100 }, default));

        var row = Assert.Single(activeAtPlan.Items, x => x.Id == active.Id);
        Assert.Equal((13, ImprovementPriority.High), (row.PriorityScore!.Value, row.Priority!.Value));
        Assert.Equal("Elif Yılmaz", row.Owner!.FullName);
        Assert.DoesNotContain(activeAtPlan.Items, x => x.Id == rejected.Id);
        Assert.Contains(rejectedOnes.Items, x => x.Id == rejected.Id && x.CurrentStep is null);
        Assert.DoesNotContain(atPilot.Items, x => x.Id == active.Id);
    }

    [Fact]
    public async Task List_NewestFirst_AndNoRequestProjects()
    {
        var requestId = await TestProjects.CreateAsync(database, "5");
        var older = await CreateAsync();
        var newer = await CreateAsync();

        var page = await AsAnalystAsync(s => s.ListAsync(new ImprovementFilter(), new PageRequest { PageSize = 100 }, default));

        var ids = page.Items.Select(x => x.Id).ToList();
        Assert.True(ids.IndexOf(newer.Id) < ids.IndexOf(older.Id));
        Assert.DoesNotContain(requestId, ids);
        Assert.All(page.Items, x => Assert.StartsWith("SI-", x.Code));
    }

    // ---- Update and sections ----

    [Fact]
    public async Task Update_ChangesTheProposal_AndNotifiesANewOwner()
    {
        var proposal = await CreateAsync();
        var managerId = await TestProjects.UserIdAsync(database, TestProjects.Manager);

        var (updated, notifications) = await AsAsync(TestProjects.Analyst, async s => await s.UpdateAsync(proposal.Id,
            AsUpdate(await NewProposalAsync()) with
            {
                Title = "Teklif onayında bekleme", Source = ImprovementSource.ManagementGoal, OwnerUserId = managerId,
                ProposedSolution = " Onay e-postası otomatik gitsin. ",
            }, default));

        Assert.Equal(proposal.Code, updated.Code);
        Assert.Equal("Teklif onayında bekleme", updated.Title);
        Assert.Equal(ImprovementSource.ManagementGoal, updated.Source);
        Assert.Equal("Onay e-postası otomatik gitsin.", updated.ProposedSolution);
        Assert.Equal("Mert Kaya", updated.Owner!.FullName);
        Assert.Equal([managerId], Assert.Single(notifications.Sent).UserIds);
        Assert.NotNull(updated.UpdatedAt);
    }

    [Fact]
    public async Task Update_SameOwner_SendsNoNotification()
    {
        var proposal = await CreateAsync();

        var (_, notifications) = await AsAsync(TestProjects.Analyst,
            async s => await s.UpdateAsync(proposal.Id, AsUpdate(await NewProposalAsync()), default));

        Assert.Empty(notifications.Sent);
    }

    [Fact]
    public async Task SaveSections_EachKeepsTheOthers()
    {
        var proposal = await CreateAsync();

        await AsAnalystAsync(s => s.SavePrioritizationAsync(proposal.Id,
            new SavePrioritizationDto { Impact = 2, Frequency = 2, Ease = 3 }, default));
        await AsAnalystAsync(s => s.SaveStudyAsync(proposal.Id, new SaveStudyDto
        {
            CurrentState = " Müşteri bilgisi iki ekranda giriliyor. ", RootCause = "Formlar ayrı tasarlanmış.",
            Solution = "Tek form", PilotPlan = "İki hafta, bir analistle", PilotTarget = "Kayıt süresi yarıya inecek",
            CostBenefit = "  ",
        }, default));
        var final = await AsAnalystAsync(s => s.SavePilotEvaluationAsync(proposal.Id, new SavePilotEvaluationDto
        {
            PilotActual = "Kayıt süresi %40 kısaldı", PilotFeedback = "Analistler memnun",
            PilotResult = PilotResult.PartiallyMet, PilotDecision = "Pilot bir kez daha tekrarlanacak",
        }, default));

        Assert.Equal((7, ImprovementPriority.Low), (final.Prioritization.Score!.Value, final.Prioritization.Priority!.Value));
        Assert.Equal("Müşteri bilgisi iki ekranda giriliyor.", final.Study.CurrentState);
        Assert.Equal("Kayıt süresi yarıya inecek", final.Study.PilotTarget);
        Assert.Null(final.Study.CostBenefit);
        Assert.Equal(PilotResult.PartiallyMet, final.PilotEvaluation.PilotResult);
        Assert.Equal("Talep kaydında mükerrer alanlar", final.Title);
    }

    [Fact]
    public async Task SavePrioritization_SomeScoresMissing_Throws400_AllEmpty_ClearsThem()
    {
        var proposal = await CreateAsync();
        await AsAnalystAsync(s => s.SavePrioritizationAsync(proposal.Id,
            new SavePrioritizationDto { Impact = 4, Frequency = 4, Ease = 4 }, default));

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => AsAnalystAsync(s => s.SavePrioritizationAsync(
            proposal.Id, new SavePrioritizationDto { Impact = 5 }, default)));
        var cleared = await AsAnalystAsync(s => s.SavePrioritizationAsync(proposal.Id, new SavePrioritizationDto(), default));

        Assert.Equal(ImprovementRules.ScoresTogether, ex.Message);
        Assert.Null(cleared.Prioritization.Score);
        Assert.Null(cleared.Prioritization.Impact);
    }

    [Theory]
    [InlineData(ProjectStatus.Rejected)]
    [InlineData(ProjectStatus.Closed)]
    public async Task Save_ProposalNotActive_Throws409(ProjectStatus status)
    {
        var proposal = await CreateAsync();
        await SetStatusAsync(proposal.Id, status);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => AsAnalystAsync(s =>
            s.SaveStudyAsync(proposal.Id, new SaveStudyDto { Solution = "Geç kaldı" }, default)));
        await Assert.ThrowsAsync<ConflictException>(async () =>
            await AsAnalystAsync(async s => await s.UpdateAsync(proposal.Id, AsUpdate(await NewProposalAsync()), default)));

        Assert.Equal("Öneri aktif değil; değiştirilemez.", ex.Message);
    }

    [Fact]
    public async Task Save_RequestProjectId_Throws404()
    {
        var requestId = await TestProjects.CreateAsync(database, "5");

        await Assert.ThrowsAsync<NotFoundException>(() => AsAnalystAsync(s =>
            s.SaveStudyAsync(requestId, new SaveStudyDto { Solution = "Yanlış kayıt" }, default)));
    }

    [Fact]
    public async Task Save_IsWrittenToTheProposalsHistory()
    {
        var proposal = await CreateAsync();
        await AsAnalystAsync(s => s.SaveStudyAsync(proposal.Id, new SaveStudyDto { RootCause = "Formlar ayrı" }, default));

        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var logs = await db.AuditLogs
            .Where(l => l.ProjectId == proposal.Id && l.EntityName == nameof(ProcessImprovement))
            .OrderBy(l => l.Id)
            .ToListAsync();

        Assert.Equal(2, logs.Count); // created, then the study
        Assert.Contains("RootCause", logs[1].Changes);
    }

    // ---- Request attributes (the API turns them into errors["impact"] ...) ----

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void PrioritizationDto_ScoreOutOfRange_HasTurkishMessage(int score)
    {
        var dto = new SavePrioritizationDto { Impact = score, Frequency = 3, Ease = 3 };
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);

        var error = Assert.Single(results);
        Assert.Equal([nameof(SavePrioritizationDto.Impact)], error.MemberNames);
        Assert.Equal("Puan 1 ile 5 arasında olmalıdır.", error.ErrorMessage);
    }

    [Fact]
    public void CreateDto_EmptyBody_ReportsEveryRequiredField()
    {
        var dto = new CreateImprovementDto();
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);

        Assert.Equal(
            ["AffectedProcess", "OwnerUserId", "Problem", "Source", "SuggestedBy", "Title"],
            results.SelectMany(r => r.MemberNames).Order());
    }
}
