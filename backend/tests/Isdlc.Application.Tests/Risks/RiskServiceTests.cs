using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Common.Models;
using Isdlc.Application.Risks;
using Isdlc.Application.Risks.Dtos;
using Isdlc.Application.Tests.Common;
using Isdlc.Domain.Audit;
using Isdlc.Domain.Identity;
using Isdlc.Domain.Projects;
using Isdlc.Domain.Risks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Isdlc.Application.Tests.Risks;

/// <summary>
/// The risk register on a real database. Every test creates its own project (TestProjects),
/// so seed projects are never changed. Expectations: docs/risks-api.md.
/// Cross test (PLANNING.md): Erva → Baran's modules.
/// </summary>
[Collection("Database")]
public class RiskServiceTests(DatabaseFixture database)
{
    private static readonly PageRequest FirstPage = new() { Page = 1, PageSize = 100 };

    /// <summary>The service as one user would call it, with notifications kept for checking.</summary>
    private async Task<(T Result, RecordingNotificationService Notifications)> AsAsync<T>(
        string? email, Func<IRiskService, Task<T>> action)
    {
        var userId = email is null ? (int?)null : await TestProjects.UserIdAsync(database, email);
        await using var scope = database.CreateScope();
        var notifications = new RecordingNotificationService();
        var service = new RiskService(
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(),
            new TestUser(userId),
            notifications);
        return (await action(service), notifications);
    }

    private async Task<int> AnalystIdAsync() => await TestProjects.UserIdAsync(database, TestProjects.Analyst);

    private static CreateRiskDto NewRisk(
        int ownerUserId,
        int probability = 3,
        int impact = 3,
        string title = "ERP servis dokümantasyonu eksik",
        RiskCategory category = RiskCategory.Technical) => new()
    {
        Title = title,
        Description = "Servislerin girdi/çıktı tanımları güncel değil.",
        Category = category,
        Probability = probability,
        Impact = impact,
        Mitigation = "Servis sahibiyle teknik çalıştay",
        OwnerUserId = ownerUserId,
        IsSharedWithRequester = true,
    };

    private static UpdateRiskDto Edit(
        RiskDto risk,
        int? ownerUserId = null,
        int? probability = null,
        int? impact = null,
        RiskStatus? status = null) => new()
    {
        Title = risk.Title,
        Description = risk.Description,
        Category = risk.Category,
        Probability = probability ?? risk.Probability,
        Impact = impact ?? risk.Impact,
        Mitigation = risk.Mitigation,
        OwnerUserId = ownerUserId ?? risk.Owner.Id,
        IsSharedWithRequester = risk.IsSharedWithRequester,
        Status = status ?? risk.Status,
    };

    /// <summary>Created by the analyst, who is also the owner, so no "assigned to you" notification.</summary>
    private async Task<RiskDto> CreateAsync(int projectId, int probability = 3, int impact = 3,
        RiskCategory category = RiskCategory.Technical)
    {
        var analystId = await AnalystIdAsync();
        return (await AsAsync(TestProjects.Analyst, s => s.CreateAsync(
            projectId, NewRisk(analystId, probability, impact, category: category), CancellationToken.None))).Result;
    }

    private async Task<RiskDto> UpdateAsync(int projectId, RiskDto risk, UpdateRiskDto request) =>
        (await AsAsync(TestProjects.Analyst, s => s.UpdateAsync(projectId, risk.Id, request, CancellationToken.None)))
        .Result;

    private async Task<PagedResult<RiskListItemDto>> ListAsync(int projectId, RiskFilter? filter = null) =>
        (await AsAsync(TestProjects.Sales, s => s.ListAsync(
            projectId, filter ?? new RiskFilter(), FirstPage, CancellationToken.None))).Result;

    private async Task<RiskMatrixDto> MatrixAsync(int projectId) =>
        (await AsAsync(TestProjects.Sales, s => s.GetMatrixAsync(projectId, CancellationToken.None))).Result;

    /// <summary>A user of this test only, so seed users stay as they are.</summary>
    private async Task<int> CreateInactiveUserAsync()
    {
        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var user = new User
        {
            FullName = "Pasif Kullanıcı",
            Email = $"pasif-{Guid.NewGuid():N}@isdlc.local",
            Role = UserRole.SoftwareDeveloper,
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private async Task RemoveProjectManagerAsync(int projectId)
    {
        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var project = await db.Projects.SingleAsync(p => p.Id == projectId);
        project.ProjectManagerId = null;
        await db.SaveChangesAsync();
    }

    // ---- Create ----

    [Fact]
    public async Task Create_FirstRisk_StartsOpenWithFirstCodeAndScore()
    {
        var projectId = await TestProjects.CreateAsync(database, "8");
        var analystId = await AnalystIdAsync();

        var risk = await CreateAsync(projectId, probability: 4, impact: 4);

        Assert.Equal("RSK-001", risk.Code);
        Assert.Equal(projectId, risk.ProjectId);
        Assert.Equal(RiskStatus.Open, risk.Status);
        Assert.Equal(16, risk.Score);
        Assert.Equal(RiskSeverity.High, risk.Severity);
        Assert.Equal(analystId, risk.Owner.Id);
        Assert.Equal("Elif Yılmaz", risk.Owner.FullName);
        Assert.Equal(analystId, risk.CreatedBy.Id);
        Assert.True(risk.IsSharedWithRequester);
        Assert.Null(risk.UpdatedAt);
    }

    [Fact]
    public async Task Create_SecondRisk_GetsNextCode()
    {
        var projectId = await TestProjects.CreateAsync(database, "8");

        await CreateAsync(projectId);
        var second = await CreateAsync(projectId);

        Assert.Equal("RSK-002", second.Code);
    }

    [Fact]
    public async Task Create_CodesAreCountedPerProject()
    {
        var firstProjectId = await TestProjects.CreateAsync(database, "8");
        var secondProjectId = await TestProjects.CreateAsync(database, "8");
        await CreateAsync(firstProjectId);
        await CreateAsync(firstProjectId);

        var risk = await CreateAsync(secondProjectId);

        Assert.Equal("RSK-001", risk.Code);
    }

    [Fact]
    public async Task Create_TrimsTitleAndTreatsBlankTextAsEmpty()
    {
        var projectId = await TestProjects.CreateAsync(database, "8");
        var analystId = await AnalystIdAsync();
        var request = NewRisk(analystId, title: "  Kilit kullanıcı ayrılabilir  ") with
        {
            Description = "   ",
            Mitigation = "",
        };

        var (risk, _) = await AsAsync(TestProjects.Analyst, s => s.CreateAsync(projectId, request, CancellationToken.None));

        Assert.Equal("Kilit kullanıcı ayrılabilir", risk.Title);
        Assert.Null(risk.Description);
        Assert.Null(risk.Mitigation);
    }

    [Fact]
    public async Task Create_InactiveOwner_ThrowsBusinessRule()
    {
        var projectId = await TestProjects.CreateAsync(database, "8");
        var inactiveUserId = await CreateInactiveUserAsync();

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => AsAsync(TestProjects.Analyst,
            s => s.CreateAsync(projectId, NewRisk(inactiveUserId), CancellationToken.None)));

        Assert.Equal("Seçilen sorumlu aktif bir kullanıcı değil.", error.Message);
    }

    [Fact]
    public async Task Create_UnknownOwner_ThrowsBusinessRule()
    {
        var projectId = await TestProjects.CreateAsync(database, "8");

        await Assert.ThrowsAsync<BusinessRuleException>(() => AsAsync(TestProjects.Analyst,
            s => s.CreateAsync(projectId, NewRisk(ownerUserId: int.MaxValue), CancellationToken.None)));
    }

    [Theory]
    [InlineData(ProjectStatus.Closed)]
    [InlineData(ProjectStatus.Rejected)]
    [InlineData(ProjectStatus.Lost)]
    [InlineData(ProjectStatus.Redirected)]
    public async Task Create_ProjectNotActive_ThrowsConflict(ProjectStatus status)
    {
        var projectId = await TestProjects.CreateAsync(database, stepCode: null, status);
        var analystId = await AnalystIdAsync();

        await Assert.ThrowsAsync<ConflictException>(() => AsAsync(TestProjects.Analyst,
            s => s.CreateAsync(projectId, NewRisk(analystId), CancellationToken.None)));
    }

    [Fact]
    public async Task Create_UnknownProject_ThrowsNotFound()
    {
        var analystId = await AnalystIdAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => AsAsync(TestProjects.Analyst,
            s => s.CreateAsync(int.MaxValue, NewRisk(analystId), CancellationToken.None)));
    }

    [Fact]
    public async Task Create_NobodySignedIn_ThrowsForbidden()
    {
        var projectId = await TestProjects.CreateAsync(database, "8");
        var analystId = await AnalystIdAsync();

        await Assert.ThrowsAsync<ForbiddenException>(() => AsAsync(null,
            s => s.CreateAsync(projectId, NewRisk(analystId), CancellationToken.None)));
    }

    [Fact]
    public async Task Create_WritesAnAuditLogForTheProject()
    {
        var projectId = await TestProjects.CreateAsync(database, "8");

        var risk = await CreateAsync(projectId);

        await using var scope = database.CreateScope();
        var log = await scope.ServiceProvider.GetRequiredService<IApplicationDbContext>().AuditLogs
            .AsNoTracking()
            .SingleAsync(l => l.EntityName == nameof(Risk) && l.EntityId == risk.Id);
        Assert.Equal(projectId, log.ProjectId);
        Assert.Equal(AuditAction.Created, log.Action);
    }

    // ---- Get ----

    [Fact]
    public async Task Get_RiskOfAnotherProject_ThrowsNotFound()
    {
        var projectId = await TestProjects.CreateAsync(database, "8");
        var otherProjectId = await TestProjects.CreateAsync(database, "8");
        var risk = await CreateAsync(projectId);

        await Assert.ThrowsAsync<NotFoundException>(() => AsAsync(TestProjects.Sales,
            s => s.GetAsync(otherProjectId, risk.Id, CancellationToken.None)));
    }

    [Fact]
    public async Task Get_ClosedProject_CanStillBeRead()
    {
        var projectId = await TestProjects.CreateAsync(database, "8");
        var risk = await CreateAsync(projectId);
        await using (var scope = database.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var project = await db.Projects.SingleAsync(p => p.Id == projectId);
            project.Status = ProjectStatus.Closed;
            project.CurrentStepId = null;
            await db.SaveChangesAsync();
        }

        var (read, _) = await AsAsync(TestProjects.Sales, s => s.GetAsync(projectId, risk.Id, CancellationToken.None));

        Assert.Equal(risk.Code, read.Code);
    }

    // ---- Update ----

    [Fact]
    public async Task Update_ChangesFieldsAndStatus()
    {
        var projectId = await TestProjects.CreateAsync(database, "8");
        var developerId = await TestProjects.UserIdAsync(database, TestProjects.Developer);
        var risk = await CreateAsync(projectId, probability: 2, impact: 2);

        var updated = await UpdateAsync(projectId, risk, Edit(risk, ownerUserId: developerId, probability: 4,
            impact: 3, status: RiskStatus.Monitoring) with { Title = "ERP servisleri gecikebilir" });

        Assert.Equal(risk.Code, updated.Code);
        Assert.Equal("ERP servisleri gecikebilir", updated.Title);
        Assert.Equal(12, updated.Score);
        Assert.Equal(RiskSeverity.Medium, updated.Severity);
        Assert.Equal(developerId, updated.Owner.Id);
        Assert.Equal(RiskStatus.Monitoring, updated.Status);
        Assert.NotNull(updated.UpdatedAt);
    }

    [Fact]
    public async Task Update_ClosedRisk_CanBeReopened()
    {
        var projectId = await TestProjects.CreateAsync(database, "8");
        var risk = await CreateAsync(projectId);
        var closed = await UpdateAsync(projectId, risk, Edit(risk, status: RiskStatus.Closed));

        var reopened = await UpdateAsync(projectId, closed, Edit(closed, status: RiskStatus.Open));

        Assert.Equal(RiskStatus.Open, reopened.Status);
    }

    [Fact]
    public async Task Update_RiskOfAnotherProject_ThrowsNotFound()
    {
        var projectId = await TestProjects.CreateAsync(database, "8");
        var otherProjectId = await TestProjects.CreateAsync(database, "8");
        var risk = await CreateAsync(projectId);

        await Assert.ThrowsAsync<NotFoundException>(() => AsAsync(TestProjects.Analyst,
            s => s.UpdateAsync(otherProjectId, risk.Id, Edit(risk), CancellationToken.None)));
    }

    [Fact]
    public async Task Update_InactiveOwner_ThrowsBusinessRuleAndKeepsTheRisk()
    {
        var projectId = await TestProjects.CreateAsync(database, "8");
        var risk = await CreateAsync(projectId);
        var inactiveUserId = await CreateInactiveUserAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() => AsAsync(TestProjects.Analyst,
            s => s.UpdateAsync(projectId, risk.Id, Edit(risk, ownerUserId: inactiveUserId, status: RiskStatus.Closed),
                CancellationToken.None)));

        var (read, _) = await AsAsync(TestProjects.Sales, s => s.GetAsync(projectId, risk.Id, CancellationToken.None));
        Assert.Equal(risk.Owner.Id, read.Owner.Id);
        Assert.Equal(RiskStatus.Open, read.Status);
    }

    [Fact]
    public async Task Update_ProjectNoLongerActive_ThrowsConflict()
    {
        var projectId = await TestProjects.CreateAsync(database, "8");
        var risk = await CreateAsync(projectId);
        await using (var scope = database.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var project = await db.Projects.SingleAsync(p => p.Id == projectId);
            project.Status = ProjectStatus.Lost;
            project.CurrentStepId = null;
            await db.SaveChangesAsync();
        }

        await Assert.ThrowsAsync<ConflictException>(() => AsAsync(TestProjects.Analyst,
            s => s.UpdateAsync(projectId, risk.Id, Edit(risk, status: RiskStatus.Closed), CancellationToken.None)));
    }

    [Fact]
    public async Task Update_NobodySignedIn_ThrowsForbidden()
    {
        var projectId = await TestProjects.CreateAsync(database, "8");
        var risk = await CreateAsync(projectId);

        await Assert.ThrowsAsync<ForbiddenException>(() => AsAsync(null,
            s => s.UpdateAsync(projectId, risk.Id, Edit(risk), CancellationToken.None)));
    }

    // ---- List ----

    [Fact]
    public async Task List_OpenRisksFirstThenHighestScoreThenCode()
    {
        var projectId = await TestProjects.CreateAsync(database, "8");
        var low = await CreateAsync(projectId, 1, 2);           // RSK-001, 2
        var high = await CreateAsync(projectId, 4, 5);          // RSK-002, 20
        var closed = await CreateAsync(projectId, 5, 5);        // RSK-003, 25, closed below
        var medium = await CreateAsync(projectId, 3, 3);        // RSK-004, 9
        var sameAsMedium = await CreateAsync(projectId, 3, 3);  // RSK-005, 9
        await UpdateAsync(projectId, closed, Edit(closed, status: RiskStatus.Closed));

        var result = await ListAsync(projectId);

        Assert.Equal(5, result.TotalCount);
        Assert.Equal(
            [high.Code, medium.Code, sameAsMedium.Code, low.Code, closed.Code],
            result.Items.Select(r => r.Code));
    }

    [Fact]
    public async Task List_FilterByStatusAndCategory_ReturnsOnlyMatches()
    {
        var projectId = await TestProjects.CreateAsync(database, "8");
        var technical = await CreateAsync(projectId, category: RiskCategory.Technical);
        var schedule = await CreateAsync(projectId, category: RiskCategory.Schedule);
        await UpdateAsync(projectId, schedule, Edit(schedule, status: RiskStatus.Monitoring));

        var byStatus = await ListAsync(projectId, new RiskFilter { Status = RiskStatus.Monitoring });
        var byCategory = await ListAsync(projectId, new RiskFilter { Category = RiskCategory.Technical });

        Assert.Equal(schedule.Code, Assert.Single(byStatus.Items).Code);
        Assert.Equal(technical.Code, Assert.Single(byCategory.Items).Code);
    }

    [Theory]
    [InlineData(RiskSeverity.Low, "RSK-001")]     // 2 × 3 = 6
    [InlineData(RiskSeverity.Medium, "RSK-002")]  // 2 × 4 = 8
    [InlineData(RiskSeverity.High, "RSK-003")]    // 3 × 5 = 15
    public async Task List_FilterBySeverity_UsesTheScoreThresholds(RiskSeverity severity, string expectedCode)
    {
        var projectId = await TestProjects.CreateAsync(database, "8");
        await CreateAsync(projectId, 2, 3);
        await CreateAsync(projectId, 2, 4);
        await CreateAsync(projectId, 3, 5);

        var result = await ListAsync(projectId, new RiskFilter { Severity = severity });

        var item = Assert.Single(result.Items);
        Assert.Equal(expectedCode, item.Code);
        Assert.Equal(severity, item.Severity);
    }

    [Fact]
    public async Task List_FilterByCell_ReturnsRisksWithThatProbabilityAndImpact()
    {
        var projectId = await TestProjects.CreateAsync(database, "8");
        await CreateAsync(projectId, 4, 2);
        var cell = await CreateAsync(projectId, 2, 4);

        var result = await ListAsync(projectId, new RiskFilter { Probability = 2, Impact = 4 });

        Assert.Equal(cell.Code, Assert.Single(result.Items).Code);
    }

    [Fact]
    public async Task List_OnlyProbabilityOrOnlyImpact_ThrowsBusinessRule()
    {
        var projectId = await TestProjects.CreateAsync(database, "8");

        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => ListAsync(projectId, new RiskFilter { Probability = 3 }));
        await Assert.ThrowsAsync<BusinessRuleException>(() => ListAsync(projectId, new RiskFilter { Impact = 3 }));

        Assert.Equal("Olasılık ve etki birlikte gönderilmelidir.", error.Message);
    }

    [Fact]
    public async Task List_Paging_ReturnsPageAndTotalCount()
    {
        var projectId = await TestProjects.CreateAsync(database, "8");
        for (var i = 0; i < 3; i++)
        {
            await CreateAsync(projectId);
        }

        var (page, _) = await AsAsync(TestProjects.Sales, s => s.ListAsync(
            projectId, new RiskFilter(), new PageRequest { Page = 2, PageSize = 2 }, CancellationToken.None));

        Assert.Equal(3, page.TotalCount);
        Assert.Equal("RSK-003", Assert.Single(page.Items).Code);
    }

    [Fact]
    public async Task List_UnknownProject_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => ListAsync(int.MaxValue));
    }

    // ---- Matrix ----

    [Fact]
    public async Task GetMatrix_CountsNotClosedRisksPerCellAndSeverity()
    {
        var projectId = await TestProjects.CreateAsync(database, "8");
        await CreateAsync(projectId, 4, 4);   // 16 High
        await CreateAsync(projectId, 3, 3);   // 9 Medium
        var monitored = await CreateAsync(projectId, 3, 3);
        await UpdateAsync(projectId, monitored, Edit(monitored, status: RiskStatus.Monitoring));
        await CreateAsync(projectId, 1, 2);   // 2 Low
        var closed = await CreateAsync(projectId, 5, 5);
        await UpdateAsync(projectId, closed, Edit(closed, status: RiskStatus.Closed));

        var matrix = await MatrixAsync(projectId);

        Assert.Equal(
            [new RiskMatrixCellDto(4, 4, 1), new RiskMatrixCellDto(3, 3, 2), new RiskMatrixCellDto(1, 2, 1)],
            matrix.Cells);
        Assert.Equal(new RiskSeverityCountsDto(Low: 1, Medium: 2, High: 1), matrix.CountBySeverity);
    }

    [Fact]
    public async Task GetMatrix_NoRisks_ReturnsEmptyCellsAndZeroCounts()
    {
        var projectId = await TestProjects.CreateAsync(database, "8");

        var matrix = await MatrixAsync(projectId);

        Assert.Empty(matrix.Cells);
        Assert.Equal(new RiskSeverityCountsDto(0, 0, 0), matrix.CountBySeverity);
    }

    [Fact]
    public async Task GetMatrix_UnknownProject_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => MatrixAsync(int.MaxValue));
    }

    // ---- Notifications ----

    [Fact]
    public async Task Create_AssignedToSomeoneElse_NotifiesOwner()
    {
        var projectId = await TestProjects.CreateAsync(database, "8");
        var developerId = await TestProjects.UserIdAsync(database, TestProjects.Developer);

        var (risk, notifications) = await AsAsync(TestProjects.Analyst, s => s.CreateAsync(
            projectId, NewRisk(developerId, probability: 2, impact: 2), CancellationToken.None));

        var sent = Assert.Single(notifications.Sent);
        Assert.Equal([developerId], sent.UserIds);
        Assert.Equal(projectId, sent.ProjectId);
        Assert.Contains($"{risk.Code} size atandı", sent.Message);
    }

    [Fact]
    public async Task Create_LowRiskOwnedBySelf_SendsNothing()
    {
        var projectId = await TestProjects.CreateAsync(database, "8");
        var analystId = await AnalystIdAsync();

        var (_, notifications) = await AsAsync(TestProjects.Analyst, s => s.CreateAsync(
            projectId, NewRisk(analystId, probability: 2, impact: 2), CancellationToken.None));

        Assert.Empty(notifications.Sent);
    }

    [Fact]
    public async Task Create_HighRisk_NotifiesProjectManagerWithScore()
    {
        var projectId = await TestProjects.CreateAsync(database, "8");
        var analystId = await AnalystIdAsync();
        var projectManagerId = await TestProjects.UserIdAsync(database, TestProjects.ProjectManager);

        var (risk, notifications) = await AsAsync(TestProjects.Analyst, s => s.CreateAsync(
            projectId, NewRisk(analystId, probability: 4, impact: 5), CancellationToken.None));

        var sent = Assert.Single(notifications.Sent);
        Assert.Equal([projectManagerId], sent.UserIds);
        Assert.Contains($"{risk.Code} yüksek risk (skor 20)", sent.Message);
    }

    [Fact]
    public async Task Create_HighRiskByTheProjectManager_DoesNotNotifyThemselves()
    {
        var projectId = await TestProjects.CreateAsync(database, "8");
        var projectManagerId = await TestProjects.UserIdAsync(database, TestProjects.ProjectManager);

        var (_, notifications) = await AsAsync(TestProjects.ProjectManager, s => s.CreateAsync(
            projectId, NewRisk(projectManagerId, probability: 5, impact: 5), CancellationToken.None));

        Assert.Empty(notifications.Sent);
    }

    [Fact]
    public async Task Create_HighRiskOnProjectWithoutManager_SendsNothing()
    {
        var projectId = await TestProjects.CreateAsync(database, "8");
        await RemoveProjectManagerAsync(projectId);
        var analystId = await AnalystIdAsync();

        var (_, notifications) = await AsAsync(TestProjects.Analyst, s => s.CreateAsync(
            projectId, NewRisk(analystId, probability: 5, impact: 5), CancellationToken.None));

        Assert.Empty(notifications.Sent);
    }

    [Fact]
    public async Task Update_RaisedToHigh_NotifiesProjectManager()
    {
        var projectId = await TestProjects.CreateAsync(database, "8");
        var projectManagerId = await TestProjects.UserIdAsync(database, TestProjects.ProjectManager);
        var risk = await CreateAsync(projectId, 3, 4);  // 12 Medium

        var (_, notifications) = await AsAsync(TestProjects.Analyst, s => s.UpdateAsync(
            projectId, risk.Id, Edit(risk, impact: 5), CancellationToken.None));  // 15 High

        var sent = Assert.Single(notifications.Sent);
        Assert.Equal([projectManagerId], sent.UserIds);
        Assert.Contains("skor 15", sent.Message);
    }

    [Fact]
    public async Task Update_AlreadyHighAndRaisedAgain_SendsNothing()
    {
        var projectId = await TestProjects.CreateAsync(database, "8");
        var risk = await CreateAsync(projectId, 4, 4);  // 16 High

        var (_, notifications) = await AsAsync(TestProjects.Analyst, s => s.UpdateAsync(
            projectId, risk.Id, Edit(risk, impact: 5), CancellationToken.None));  // 20 High

        Assert.Empty(notifications.Sent);
    }

    [Fact]
    public async Task Update_ClosedHighRiskReopened_NotifiesProjectManager()
    {
        var projectId = await TestProjects.CreateAsync(database, "8");
        var projectManagerId = await TestProjects.UserIdAsync(database, TestProjects.ProjectManager);
        var risk = await CreateAsync(projectId, 4, 4);
        var closed = await UpdateAsync(projectId, risk, Edit(risk, status: RiskStatus.Closed));

        var (_, notifications) = await AsAsync(TestProjects.Analyst, s => s.UpdateAsync(
            projectId, closed.Id, Edit(closed, status: RiskStatus.Open), CancellationToken.None));

        Assert.Equal([projectManagerId], Assert.Single(notifications.Sent).UserIds);
    }

    [Fact]
    public async Task Update_OwnerChanged_NotifiesNewOwnerOnly()
    {
        var projectId = await TestProjects.CreateAsync(database, "8");
        var developerId = await TestProjects.UserIdAsync(database, TestProjects.Developer);
        var risk = await CreateAsync(projectId, 2, 2);

        var (_, notifications) = await AsAsync(TestProjects.Analyst, s => s.UpdateAsync(
            projectId, risk.Id, Edit(risk, ownerUserId: developerId), CancellationToken.None));

        var sent = Assert.Single(notifications.Sent);
        Assert.Equal([developerId], sent.UserIds);
        Assert.Contains("size atandı", sent.Message);
    }

    [Fact]
    public async Task Update_SameOwner_SendsNothing()
    {
        var projectId = await TestProjects.CreateAsync(database, "8");
        var developerId = await TestProjects.UserIdAsync(database, TestProjects.Developer);
        var (risk, _) = await AsAsync(TestProjects.Analyst, s => s.CreateAsync(
            projectId, NewRisk(developerId, probability: 2, impact: 2), CancellationToken.None));

        var (_, notifications) = await AsAsync(TestProjects.Analyst, s => s.UpdateAsync(
            projectId, risk.Id, Edit(risk, status: RiskStatus.Monitoring), CancellationToken.None));

        Assert.Empty(notifications.Sent);
    }
}
