using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Common.Models;
using Isdlc.Application.Projects;
using Isdlc.Application.Projects.Dtos;
using Isdlc.Application.Tests.Common;
using Isdlc.Domain.Identity;
using Isdlc.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Isdlc.Application.Tests.Projects;

/// <summary>
/// Requests and projects on a real database. Expectations: docs/projects-api.md.
/// Every test opens requests as a Business Analyst of its own and assigns them to that analyst, so the requests do
/// not show up in other tests' "my work" lists and seed projects are never changed.
/// Codes are tested in far years (2090, 2091…) so the real numbering of this year is not affected.
/// Cross test (PLANNING.md): Baran → Dide's modules.
/// </summary>
[Collection("Database")]
public class ProjectServiceTests(DatabaseFixture database)
{
    private static readonly PageRequest FirstPage = new() { Page = 1, PageSize = 100 };

    /// <summary>The service as one user would call it, with notifications kept for checking.</summary>
    private async Task<(T Result, RecordingNotificationService Notifications)> AsAsync<T>(
        int? userId, Func<IProjectService, Task<T>> action, TimeProvider? time = null)
    {
        await using var scope = database.CreateScope();
        var notifications = new RecordingNotificationService();
        var service = new ProjectService(
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(),
            new TestUser(userId),
            notifications,
            time ?? TimeProvider.System);
        return (await action(service), notifications);
    }

    private async Task<ProjectDto> CreateAsync(int userId, CreateProjectDto request, TimeProvider? time = null) =>
        (await AsAsync(userId, s => s.CreateAsync(request, CancellationToken.None), time)).Result;

    private async Task<PagedResult<ProjectListItemDto>> ListAsync(ProjectFilter filter) =>
        (await AsAsync(null, s => s.ListAsync(filter, FirstPage, CancellationToken.None))).Result;

    /// <summary>A user of this test only, so seed users are never changed.</summary>
    private async Task<int> NewUserAsync(UserRole role, bool isActive = true, string? fullName = null)
    {
        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var user = new User
        {
            FullName = fullName ?? $"Test {role}",
            Email = $"projects-{Guid.NewGuid():N}@isdlc.local",
            Role = role,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    /// <summary>A valid request assigned to the given analyst; every test changes only what it checks.</summary>
    private static CreateProjectDto Request(int analystId, string? name = null) =>
        CreateProjectDtoTests.Valid() with { Name = name ?? "Bayi Sipariş Portalı", BusinessAnalystId = analystId };

    /// <summary>A project written straight to the database, for list and code tests that need a given state.</summary>
    private async Task<int> InsertProjectAsync(
        int analystId, string? stepCode, ProjectStatus status = ProjectStatus.Active, string? code = null,
        string name = "Liste testi")
    {
        int? stepId = stepCode is null ? null : await TestProjects.StepIdAsync(database, stepCode);
        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var project = new Project
        {
            Code = code ?? $"T-{Guid.NewGuid():N}"[..14],
            Name = name,
            Description = "Test için oluşturuldu.",
            Source = RequestSource.Internal,
            RequestChannel = RequestChannel.Written,
            RequestType = RequestType.NewProject,
            CustomerName = "Test",
            RequesterName = "Test",
            BusinessAnalystId = analystId,
            Status = status,
            CurrentStepId = stepId,
            StartDate = new DateOnly(2026, 11, 1),
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        return project.Id;
    }

    /// <summary>"Now" in a chosen year, so code tests have the year's numbering to themselves.</summary>
    private sealed class FixedYear(int year) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(year, 6, 1, 12, 0, 0, TimeSpan.Zero);
    }

    // ---- Create ----

    [Fact]
    public async Task Create_AsBusinessAnalyst_StartsActiveAtFirstStepWithoutPriorityOrLevel()
    {
        var analyst = await NewUserAsync(UserRole.BusinessAnalyst, fullName: "Test Analist");

        var project = await CreateAsync(analyst, Request(analyst));

        Assert.Equal(ProjectStatus.Active, project.Status);
        Assert.NotNull(project.CurrentStep);
        Assert.Equal("1.1", project.CurrentStep.Code);
        Assert.Equal(1, project.CurrentStep.StageOrder);
        Assert.Null(project.Priority);
        Assert.Null(project.Level);
        Assert.StartsWith(ProjectCodes.YearPrefix(DateTime.UtcNow.Year), project.Code);
        // CreatedBy is not checked here: AuditableEntityInterceptor takes the user from the fixture's DI container
        // (Elif Yılmaz), not from the TestUser given to the service. In the API both are the signed-in user.
        Assert.Equal(new UserSummaryDto(analyst, "Test Analist"), project.BusinessAnalyst);
        Assert.Null(project.ProjectManager);
    }

    [Fact]
    public async Task Create_TextWithSpaces_IsSavedTrimmed()
    {
        var analyst = await NewUserAsync(UserRole.BusinessAnalyst);

        var project = await CreateAsync(analyst, Request(analyst) with
        {
            Name = "  Bayi Portalı  ",
            Description = "  Açıklama  ",
            CustomerName = "  Ege Gıda  ",
            RequesterName = "  Satış Direktörü  ",
        });

        Assert.Equal("Bayi Portalı", project.Name);
        Assert.Equal("Açıklama", project.Description);
        Assert.Equal("Ege Gıda", project.CustomerName);
        Assert.Equal("Satış Direktörü", project.RequesterName);
    }

    [Fact]
    public async Task Create_ReturnsWhatGetReturns()
    {
        var analyst = await NewUserAsync(UserRole.BusinessAnalyst);
        var manager = await NewUserAsync(UserRole.ProjectManager);

        var created = await CreateAsync(analyst, Request(analyst) with { ProjectManagerId = manager, RequiresNda = true });
        var read = (await AsAsync(null, s => s.GetAsync(created.Id, CancellationToken.None))).Result;

        Assert.Equal(created, read);
        Assert.True(read.RequiresNda);
        Assert.Equal(manager, read.ProjectManager?.Id);
    }

    [Fact]
    public async Task Create_FirstRequestsOfAYear_AreNumberedFrom001()
    {
        var analyst = await NewUserAsync(UserRole.BusinessAnalyst);
        var year2090 = new FixedYear(2090);

        var first = await CreateAsync(analyst, Request(analyst), year2090);
        var second = await CreateAsync(analyst, Request(analyst), year2090);

        Assert.Equal("ISDLC-2090-001", first.Code);
        Assert.Equal("ISDLC-2090-002", second.Code);
    }

    [Fact]
    public async Task Create_AfterCode1000_Gets1001NotA999Successor()
    {
        // Ordering by text alone would take "-999" as the highest code and give the taken "-1000" again.
        var analyst = await NewUserAsync(UserRole.BusinessAnalyst);
        await InsertProjectAsync(analyst, "1.1", code: "ISDLC-2091-999");
        await InsertProjectAsync(analyst, "1.1", code: "ISDLC-2091-1000");

        var project = await CreateAsync(analyst, Request(analyst), new FixedYear(2091));

        Assert.Equal("ISDLC-2091-1001", project.Code);
    }

    [Fact]
    public async Task Create_NewYear_StartsAgainFrom001()
    {
        var analyst = await NewUserAsync(UserRole.BusinessAnalyst);
        await CreateAsync(analyst, Request(analyst), new FixedYear(2092));

        var nextYear = await CreateAsync(analyst, Request(analyst), new FixedYear(2093));

        Assert.Equal("ISDLC-2093-001", nextYear.Code);
    }

    [Theory]
    [InlineData(UserRole.SoftwareDepartmentManager)]
    [InlineData(UserRole.ProjectManager)]
    [InlineData(UserRole.SoftwareDeveloper)]
    [InlineData(UserRole.TestSpecialist)]
    [InlineData(UserRole.SalesRepresentative)]
    public async Task Create_NotABusinessAnalyst_Throws403(UserRole role)
    {
        var analyst = await NewUserAsync(UserRole.BusinessAnalyst);
        var someone = await NewUserAsync(role);

        var error = await Assert.ThrowsAsync<ForbiddenException>(() => CreateAsync(someone, Request(analyst)));

        Assert.Equal("Talebi yalnızca İş Analisti açabilir.", error.Message);
    }

    [Fact]
    public async Task Create_InactiveBusinessAnalyst_Throws403()
    {
        var inactive = await NewUserAsync(UserRole.BusinessAnalyst, isActive: false);

        await Assert.ThrowsAsync<ForbiddenException>(() => CreateAsync(inactive, Request(inactive)));
    }

    [Fact]
    public async Task Create_NotSignedIn_Throws403()
    {
        var analyst = await NewUserAsync(UserRole.BusinessAnalyst);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => AsAsync(null, s => s.CreateAsync(Request(analyst), CancellationToken.None)));
    }

    [Fact]
    public async Task Create_AnalystFieldIsNotABusinessAnalyst_Throws400()
    {
        var analyst = await NewUserAsync(UserRole.BusinessAnalyst);
        var developer = await NewUserAsync(UserRole.SoftwareDeveloper);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => CreateAsync(analyst, Request(analyst) with { BusinessAnalystId = developer }));

        Assert.Equal("Seçilen iş analisti aktif bir İş Analisti değil.", error.Message);
    }

    [Fact]
    public async Task Create_ManagerFieldIsNotAProjectManager_Throws400()
    {
        var analyst = await NewUserAsync(UserRole.BusinessAnalyst);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => CreateAsync(analyst, Request(analyst) with { ProjectManagerId = analyst }));

        Assert.Equal("Seçilen proje yöneticisi aktif bir Proje Yöneticisi değil.", error.Message);
    }

    [Fact]
    public async Task Create_InactiveProjectManager_Throws400()
    {
        var analyst = await NewUserAsync(UserRole.BusinessAnalyst);
        var inactiveManager = await NewUserAsync(UserRole.ProjectManager, isActive: false);

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => CreateAsync(analyst, Request(analyst) with { ProjectManagerId = inactiveManager }));
    }

    [Fact]
    public async Task Create_RejectedRequest_SavesNothing()
    {
        var analyst = await NewUserAsync(UserRole.BusinessAnalyst);
        var uniqueName = $"Kaydedilmemeli {Guid.NewGuid():N}";

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => CreateAsync(analyst, Request(analyst, uniqueName) with { ProjectManagerId = analyst }));

        Assert.Equal(0, (await ListAsync(new ProjectFilter { Search = uniqueName })).TotalCount);
    }

    [Fact]
    public async Task Create_AssignsAnotherAnalystAndAManager_NotifiesBoth()
    {
        var opener = await NewUserAsync(UserRole.BusinessAnalyst);
        var analyst = await NewUserAsync(UserRole.BusinessAnalyst);
        var manager = await NewUserAsync(UserRole.ProjectManager);

        var (project, notifications) = await AsAsync(opener, s => s.CreateAsync(
            Request(analyst, "Bayi Portalı") with { ProjectManagerId = manager }, CancellationToken.None));

        Assert.Equal(2, notifications.Sent.Count);
        Assert.Contains(notifications.Sent, n => n.UserIds.SequenceEqual([analyst]) && n.ProjectId == project.Id
            && n.Message == $"{project.Code} · Bayi Portalı projesine iş analisti olarak atandınız.");
        Assert.Contains(notifications.Sent, n => n.UserIds.SequenceEqual([manager])
            && n.Message == $"{project.Code} · Bayi Portalı projesine proje yöneticisi olarak atandınız.");
    }

    [Fact]
    public async Task Create_AssignsThemselves_NotifiesNobody()
    {
        var analyst = await NewUserAsync(UserRole.BusinessAnalyst);

        var (_, notifications) = await AsAsync(analyst, s => s.CreateAsync(Request(analyst), CancellationToken.None));

        Assert.Empty(notifications.Sent);
    }

    // ---- Get ----

    [Fact]
    public async Task Get_MissingProject_Throws404()
    {
        var error = await Assert.ThrowsAsync<NotFoundException>(
            () => AsAsync(null, s => s.GetAsync(int.MaxValue, CancellationToken.None)));

        Assert.Equal("Proje bulunamadı.", error.Message);
    }

    [Fact]
    public async Task Get_ClosedProject_HasNoCurrentStep()
    {
        var analyst = await NewUserAsync(UserRole.BusinessAnalyst);
        var projectId = await InsertProjectAsync(analyst, stepCode: null, ProjectStatus.Lost);

        var project = (await AsAsync(null, s => s.GetAsync(projectId, CancellationToken.None))).Result;

        Assert.Equal(ProjectStatus.Lost, project.Status);
        Assert.Null(project.CurrentStep);
    }

    // ---- Update ----

    private static UpdateProjectDto UpdateOf(CreateProjectDto request) => new()
    {
        Name = request.Name,
        Description = request.Description,
        Source = request.Source,
        RequestChannel = request.RequestChannel,
        RequestType = request.RequestType,
        CustomerName = request.CustomerName,
        RequesterName = request.RequesterName,
        RequiresNda = request.RequiresNda,
        BusinessAnalystId = request.BusinessAnalystId,
        ProjectManagerId = request.ProjectManagerId,
        StartDate = request.StartDate,
        DueDate = request.DueDate,
    };

    [Fact]
    public async Task Update_AnyActiveUser_ChangesTheInformationButNotCodeStatusOrStep()
    {
        // Decision 1: editing is open to every active user, not only analysts.
        var analyst = await NewUserAsync(UserRole.BusinessAnalyst);
        var developer = await NewUserAsync(UserRole.SoftwareDeveloper);
        var created = await CreateAsync(analyst, Request(analyst));

        var update = UpdateOf(Request(analyst)) with
        {
            Name = "Yeni ad",
            CustomerName = "Yeni müşteri",
            RequestType = RequestType.AdditionalModule,
            DueDate = new DateOnly(2027, 6, 1),
        };
        var updated = (await AsAsync(developer, s => s.UpdateAsync(created.Id, update, CancellationToken.None))).Result;

        Assert.Equal("Yeni ad", updated.Name);
        Assert.Equal("Yeni müşteri", updated.CustomerName);
        Assert.Equal(RequestType.AdditionalModule, updated.RequestType);
        Assert.Equal(new DateOnly(2027, 6, 1), updated.DueDate);
        Assert.Equal(created.Code, updated.Code);
        Assert.Equal(created.Status, updated.Status);
        Assert.Equal(created.CurrentStep, updated.CurrentStep);
        Assert.Equal(created.CreatedBy, updated.CreatedBy);
        Assert.NotNull(updated.UpdatedAt);
    }

    [Theory]
    [InlineData(ProjectStatus.Closed)]
    [InlineData(ProjectStatus.Rejected)]
    [InlineData(ProjectStatus.Lost)]
    [InlineData(ProjectStatus.Redirected)]
    public async Task Update_ProjectNotActive_Throws409(ProjectStatus status)
    {
        var analyst = await NewUserAsync(UserRole.BusinessAnalyst);
        var projectId = await InsertProjectAsync(analyst, stepCode: null, status);

        var error = await Assert.ThrowsAsync<ConflictException>(() => AsAsync(analyst,
            s => s.UpdateAsync(projectId, UpdateOf(Request(analyst)), CancellationToken.None)));

        Assert.Equal("Proje aktif değil; bilgileri değiştirilemez.", error.Message);
    }

    [Fact]
    public async Task Update_MissingProject_Throws404()
    {
        var analyst = await NewUserAsync(UserRole.BusinessAnalyst);

        await Assert.ThrowsAsync<NotFoundException>(() => AsAsync(analyst,
            s => s.UpdateAsync(int.MaxValue, UpdateOf(Request(analyst)), CancellationToken.None)));
    }

    [Fact]
    public async Task Update_NotSignedIn_Throws403()
    {
        var analyst = await NewUserAsync(UserRole.BusinessAnalyst);
        var created = await CreateAsync(analyst, Request(analyst));

        await Assert.ThrowsAsync<ForbiddenException>(() => AsAsync(null,
            s => s.UpdateAsync(created.Id, UpdateOf(Request(analyst)), CancellationToken.None)));
    }

    [Fact]
    public async Task Update_ManagerIsNotAProjectManager_Throws400AndChangesNothing()
    {
        var analyst = await NewUserAsync(UserRole.BusinessAnalyst);
        var created = await CreateAsync(analyst, Request(analyst));

        await Assert.ThrowsAsync<BusinessRuleException>(() => AsAsync(analyst, s => s.UpdateAsync(
            created.Id, UpdateOf(Request(analyst)) with { Name = "Değişmemeli", ProjectManagerId = analyst },
            CancellationToken.None)));

        var read = (await AsAsync(null, s => s.GetAsync(created.Id, CancellationToken.None))).Result;
        Assert.Equal(created.Name, read.Name);
    }

    [Fact]
    public async Task Update_NewManager_NotifiesOnlyTheNewlyAssignedPerson()
    {
        var analyst = await NewUserAsync(UserRole.BusinessAnalyst);
        var manager = await NewUserAsync(UserRole.ProjectManager);
        var created = await CreateAsync(analyst, Request(analyst));

        var (_, notifications) = await AsAsync(analyst, s => s.UpdateAsync(
            created.Id, UpdateOf(Request(analyst)) with { ProjectManagerId = manager }, CancellationToken.None));

        // The analyst did not change, so only the new project manager hears about it.
        var sent = Assert.Single(notifications.Sent);
        Assert.Equal([manager], sent.UserIds);
    }

    [Fact]
    public async Task Update_SamePeople_NotifiesNobody()
    {
        var opener = await NewUserAsync(UserRole.BusinessAnalyst);
        var analyst = await NewUserAsync(UserRole.BusinessAnalyst);
        var created = await CreateAsync(opener, Request(analyst));

        var (_, notifications) = await AsAsync(opener, s => s.UpdateAsync(
            created.Id, UpdateOf(Request(analyst)) with { Name = "Yalnızca ad değişti" }, CancellationToken.None));

        Assert.Empty(notifications.Sent);
    }

    // ---- List ----

    [Fact]
    public async Task List_Phases_SplitEveryRecordInTwo()
    {
        var analyst = await NewUserAsync(UserRole.BusinessAnalyst);
        var stage1 = await InsertProjectAsync(analyst, "1.1");
        var stage3 = await InsertProjectAsync(analyst, "3.2");
        var stage4 = await InsertProjectAsync(analyst, "4.1");
        var redirectedEarly = await InsertProjectAsync(analyst, stepCode: null, ProjectStatus.Redirected);
        var closed = await InsertProjectAsync(analyst, stepCode: null, ProjectStatus.Closed);

        var requests = await ListAsync(new ProjectFilter { BusinessAnalystId = analyst, Phase = ProjectPhase.Request });
        var projects = await ListAsync(new ProjectFilter { BusinessAnalystId = analyst, Phase = ProjectPhase.Project });
        var activeProjects = await ListAsync(new ProjectFilter
        {
            BusinessAnalystId = analyst, Phase = ProjectPhase.Project, Status = ProjectStatus.Active,
        });

        Assert.Equal([stage1, stage3], requests.Items.Select(p => p.Id).Order());
        Assert.Equal([stage4, redirectedEarly, closed], projects.Items.Select(p => p.Id).Order());
        Assert.Equal([stage4], activeProjects.Items.Select(p => p.Id));
        // Every record is on exactly one of the two lists.
        Assert.Equal(5, requests.TotalCount + projects.TotalCount);
    }

    [Fact]
    public async Task List_NoPhase_ShowsEverything()
    {
        var analyst = await NewUserAsync(UserRole.BusinessAnalyst);
        await InsertProjectAsync(analyst, "1.1");
        await InsertProjectAsync(analyst, "4.1");
        await InsertProjectAsync(analyst, stepCode: null, ProjectStatus.Rejected);

        Assert.Equal(3, (await ListAsync(new ProjectFilter { BusinessAnalystId = analyst })).TotalCount);
    }

    [Fact]
    public async Task List_Status_KeepsOnlyThatStatus()
    {
        var analyst = await NewUserAsync(UserRole.BusinessAnalyst);
        await InsertProjectAsync(analyst, "4.1");
        var lost = await InsertProjectAsync(analyst, stepCode: null, ProjectStatus.Lost);

        var result = await ListAsync(new ProjectFilter { BusinessAnalystId = analyst, Status = ProjectStatus.Lost });

        Assert.Equal([lost], result.Items.Select(p => p.Id));
    }

    [Fact]
    public async Task List_Search_FindsCodeNameOrCustomerIgnoringCase()
    {
        var analyst = await NewUserAsync(UserRole.BusinessAnalyst);
        var token = Guid.NewGuid().ToString("N")[..10];
        var created = await CreateAsync(analyst, Request(analyst, $"Portal {token} Sipariş") with
        {
            CustomerName = $"Müşteri {token.ToUpperInvariant()}",
        });

        var byName = await ListAsync(new ProjectFilter { Search = $"portal {token}" });
        // Stored as "Müşteri ABC…", searched as "müşteri abc…".
        var byCustomer = await ListAsync(new ProjectFilter { Search = $"müşteri {token}" });
        var byCode = await ListAsync(new ProjectFilter { Search = created.Code });

        Assert.Equal([created.Id], byName.Items.Select(p => p.Id));
        Assert.Contains(byCustomer.Items, p => p.Id == created.Id);
        Assert.Equal([created.Id], byCode.Items.Select(p => p.Id));
    }

    [Fact]
    public async Task List_SearchWithSpaces_IsTrimmed()
    {
        var analyst = await NewUserAsync(UserRole.BusinessAnalyst);
        var token = Guid.NewGuid().ToString("N")[..10];
        var created = await CreateAsync(analyst, Request(analyst, $"Ad {token}"));

        var result = await ListAsync(new ProjectFilter { Search = $"   {token}   " });

        Assert.Equal([created.Id], result.Items.Select(p => p.Id));
    }

    [Fact]
    public async Task List_NewestRequestFirst_EvenAfterCode999()
    {
        var analyst = await NewUserAsync(UserRole.BusinessAnalyst);
        var code998 = await InsertProjectAsync(analyst, "1.1", code: "ISDLC-2094-998");
        var code1000 = await InsertProjectAsync(analyst, "1.1", code: "ISDLC-2094-1000");
        var code999 = await InsertProjectAsync(analyst, "1.1", code: "ISDLC-2094-999");

        var result = await ListAsync(new ProjectFilter { BusinessAnalystId = analyst });

        Assert.Equal([code1000, code999, code998], result.Items.Select(p => p.Id));
    }

    [Fact]
    public async Task List_Paging_ReturnsThePageAndTheTotal()
    {
        var analyst = await NewUserAsync(UserRole.BusinessAnalyst);
        for (var i = 1; i <= 5; i++)
        {
            await InsertProjectAsync(analyst, "1.1", code: $"ISDLC-2095-{i:D3}");
        }

        var page2 = (await AsAsync(null, s => s.ListAsync(
            new ProjectFilter { BusinessAnalystId = analyst }, new PageRequest { Page = 2, PageSize = 2 },
            CancellationToken.None))).Result;

        Assert.Equal(5, page2.TotalCount);
        Assert.Equal(["ISDLC-2095-003", "ISDLC-2095-002"], page2.Items.Select(p => p.Code));
    }

    [Fact]
    public async Task List_Row_ShowsCurrentStepAndAnalyst()
    {
        var analyst = await NewUserAsync(UserRole.BusinessAnalyst, fullName: "Liste Analisti");
        await InsertProjectAsync(analyst, "4.1");

        var row = Assert.Single((await ListAsync(new ProjectFilter { BusinessAnalystId = analyst })).Items);

        Assert.Equal("4.1", row.CurrentStep?.Code);
        Assert.Equal(4, row.CurrentStep?.StageOrder);
        Assert.Equal(new UserSummaryDto(analyst, "Liste Analisti"), row.BusinessAnalyst);
    }
}
