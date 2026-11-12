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
/// Proje No is shared by every test, so number tests check "one more than the highest", never a fixed number.
/// Cross test (PLANNING.md): Baran → Dide's modules.
/// </summary>
[Collection("Database")]
public class ProjectServiceTests(DatabaseFixture database)
{
    private static readonly PageRequest FirstPage = new() { Page = 1, PageSize = 100 };

    /// <summary>The service as one user would call it, with notifications kept for checking.</summary>
    private async Task<(T Result, RecordingNotificationService Notifications)> AsAsync<T>(
        int? userId, Func<IProjectService, Task<T>> action)
    {
        await using var scope = database.CreateScope();
        var notifications = new RecordingNotificationService();
        var service = new ProjectService(
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(),
            new TestUser(userId),
            notifications);
        return (await action(service), notifications);
    }

    private async Task<ProjectDto> CreateAsync(int userId, CreateProjectDto request) =>
        (await AsAsync(userId, s => s.CreateAsync(request, CancellationToken.None))).Result;

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
        string name = "Liste testi", RequestSource source = RequestSource.Internal, bool requiresOffer = true)
    {
        int? stepId = stepCode is null ? null : await TestProjects.StepIdAsync(database, stepCode);
        var flowId = await TestProjects.FlowIdAsync(database);
        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var project = new Project
        {
            Code = code ?? $"T-{Guid.NewGuid():N}"[..14],
            Name = name,
            Description = "Test için oluşturuldu.",
            Source = source,
            RequestChannel = RequestChannel.Email,
            RequestType = RequestType.NewProject,
            CustomerName = "Test",
            RequesterName = "Test",
            BusinessAnalystId = analystId,
            Status = status,
            ProcessFlowId = flowId,
            RequiresOffer = requiresOffer,
            CurrentStepId = stepId,
            RequestDate = new DateOnly(2026, 11, 1),
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        return project.Id;
    }

    // ---- Create ----

    [Fact]
    public async Task Create_AsBusinessAnalyst_StartsActiveAtFirstStepWithoutPriorityOrLevel()
    {
        var analyst = await NewUserAsync(UserRole.BusinessAnalyst, fullName: "Test Analist");

        var project = await CreateAsync(analyst, Request(analyst));

        Assert.Equal(ProjectStatus.Active, project.Status);
        Assert.NotNull(project.CurrentStep);
        Assert.Equal("1", project.CurrentStep.Code);
        Assert.Equal(1, project.CurrentStep.StageOrder);
        Assert.Null(project.Priority);
        Assert.Null(project.Level);
        Assert.Matches("^[1-9][0-9]*$", project.Code);
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
            RequesterContact = "  0232 000 00 00  ",
            Notes = "  Not  ",
        });

        Assert.Equal("Bayi Portalı", project.Name);
        Assert.Equal("Açıklama", project.Description);
        Assert.Equal("Ege Gıda", project.CustomerName);
        Assert.Equal("Satış Direktörü", project.RequesterName);
        Assert.Equal("0232 000 00 00", project.RequesterContact);
        Assert.Equal("Not", project.Notes);
    }

    [Fact]
    public async Task Create_OptionalTextOnlySpaces_IsSavedAsEmpty()
    {
        var analyst = await NewUserAsync(UserRole.BusinessAnalyst);

        var project = await CreateAsync(analyst, Request(analyst) with { RequesterContact = "   ", Notes = " " });

        Assert.Null(project.RequesterContact);
        Assert.Null(project.Notes);
    }

    [Fact]
    public async Task Create_SavesTheRequestFormFields()
    {
        var analyst = await NewUserAsync(UserRole.BusinessAnalyst);

        var project = await CreateAsync(analyst, Request(analyst) with
        {
            Source = RequestSource.Affiliated,
            RequestChannel = RequestChannel.Phone,
            RequesterContact = "bilgi@ornek.com",
            Notes = "Toplantıda iletildi.",
            RequiresOffer = false,
            RequestDate = new DateOnly(2026, 11, 3),
        });

        Assert.Equal(RequestSource.Affiliated, project.Source);
        Assert.Equal(RequestChannel.Phone, project.RequestChannel);
        Assert.Equal("bilgi@ornek.com", project.RequesterContact);
        Assert.Equal("Toplantıda iletildi.", project.Notes);
        Assert.False(project.RequiresOffer);
        Assert.Equal(new DateOnly(2026, 11, 3), project.RequestDate);
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
    public async Task Create_ProjectNo_IsOneMoreThanTheHighest()
    {
        var analyst = await NewUserAsync(UserRole.BusinessAnalyst);

        var first = await CreateAsync(analyst, Request(analyst));
        var second = await CreateAsync(analyst, Request(analyst));

        Assert.Equal(long.Parse(first.Code) + 1, long.Parse(second.Code));
    }

    [Fact]
    public async Task Create_AfterANumberWithMoreDigits_GetsItsSuccessorNotA999Successor()
    {
        // Ordering by text alone would take "999…" as the highest number and give the taken "1000…" again.
        var analyst = await NewUserAsync(UserRole.BusinessAnalyst);
        var digits = (await CreateAsync(analyst, Request(analyst))).Code.Length + 1;
        var nines = new string('9', digits);            // e.g. "999"
        var power = "1" + new string('0', digits);      // e.g. "1000"
        await InsertProjectAsync(analyst, "1", code: nines);
        await InsertProjectAsync(analyst, "1", code: power);

        var project = await CreateAsync(analyst, Request(analyst));

        Assert.Equal(long.Parse(power) + 1, long.Parse(project.Code));
    }

    [Fact]
    public async Task Create_RecordsWithOtherCodes_DoNotChangeTheNumbering()
    {
        // Test records written straight to the database have codes like "T-…"; they are not Proje No values.
        var analyst = await NewUserAsync(UserRole.BusinessAnalyst);
        var before = await CreateAsync(analyst, Request(analyst));
        await InsertProjectAsync(analyst, "1", code: $"ZZ-{Guid.NewGuid():N}"[..19]);

        var after = await CreateAsync(analyst, Request(analyst));

        Assert.Equal(long.Parse(before.Code) + 1, long.Parse(after.Code));
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
        RequesterContact = request.RequesterContact,
        Notes = request.Notes,
        RequiresOffer = request.RequiresOffer,
        RequiresNda = request.RequiresNda,
        BusinessAnalystId = request.BusinessAnalystId,
        ProjectManagerId = request.ProjectManagerId,
        RequestDate = request.RequestDate,
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

    // ---- Source and offer (decision D1-3) ----

    /// <summary>The update of a record written straight to the database, with only source and offer changed.</summary>
    private async Task<ProjectDto> UpdateSourceAndOfferAsync(
        int analystId, int projectId, RequestSource source, bool requiresOffer) =>
        (await AsAsync(analystId, s => s.UpdateAsync(projectId, UpdateOf(Request(analystId)) with
        {
            Source = source,
            RequiresOffer = requiresOffer,
        }, CancellationToken.None))).Result;

    [Theory]
    [InlineData("1")]
    [InlineData("3")]
    public async Task Update_SourceAndOffer_BeforeStep4_Change(string stepCode)
    {
        var analyst = await NewUserAsync(UserRole.BusinessAnalyst);
        var projectId = await InsertProjectAsync(analyst, stepCode, source: RequestSource.Internal, requiresOffer: true);

        var updated = await UpdateSourceAndOfferAsync(analyst, projectId, RequestSource.External, requiresOffer: false);

        Assert.Equal(RequestSource.External, updated.Source);
        Assert.False(updated.RequiresOffer);
    }

    [Theory]
    [InlineData("4", RequestSource.Internal, true)]  // the step itself: source changes
    [InlineData("4", RequestSource.External, false)] // the step itself: offer changes
    [InlineData("10", RequestSource.External, false)]
    [InlineData("14", RequestSource.Internal, true)]
    public async Task Update_SourceOrOffer_FromStep4On_Throws409(string stepCode, RequestSource source, bool requiresOffer)
    {
        var analyst = await NewUserAsync(UserRole.BusinessAnalyst);
        var projectId = await InsertProjectAsync(analyst, stepCode, source: RequestSource.External, requiresOffer: true);

        var error = await Assert.ThrowsAsync<ConflictException>(
            () => UpdateSourceAndOfferAsync(analyst, projectId, source, requiresOffer));

        Assert.Equal("Talep kaynağı ve teklif bilgisi 4. adıma gelindikten sonra değiştirilemez.", error.Message);
    }

    [Fact]
    public async Task Update_SameSourceAndOfferAfterStep4_OtherFieldsStillChange()
    {
        // Sending the same values again is not a change, so the rest of the form can still be saved.
        var analyst = await NewUserAsync(UserRole.BusinessAnalyst);
        var projectId = await InsertProjectAsync(analyst, "10", source: RequestSource.External, requiresOffer: true);

        var updated = (await AsAsync(analyst, s => s.UpdateAsync(projectId, UpdateOf(Request(analyst)) with
        {
            Source = RequestSource.External,
            RequiresOffer = true,
            Notes = "Teklif görüşmesi yapıldı.",
        }, CancellationToken.None))).Result;

        Assert.Equal("Teklif görüşmesi yapıldı.", updated.Notes);
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
        var stage1 = await InsertProjectAsync(analyst, "1");
        var stage3 = await InsertProjectAsync(analyst, "4");
        var stage4 = await InsertProjectAsync(analyst, "12");
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
        await InsertProjectAsync(analyst, "1");
        await InsertProjectAsync(analyst, "12");
        await InsertProjectAsync(analyst, stepCode: null, ProjectStatus.Rejected);

        Assert.Equal(3, (await ListAsync(new ProjectFilter { BusinessAnalystId = analyst })).TotalCount);
    }

    [Fact]
    public async Task List_Status_KeepsOnlyThatStatus()
    {
        var analyst = await NewUserAsync(UserRole.BusinessAnalyst);
        await InsertProjectAsync(analyst, "12");
        var lost = await InsertProjectAsync(analyst, stepCode: null, ProjectStatus.Lost);

        var result = await ListAsync(new ProjectFilter { BusinessAnalystId = analyst, Status = ProjectStatus.Lost });

        Assert.Equal([lost], result.Items.Select(p => p.Id));
    }

    [Fact]
    public async Task List_Search_FindsProjectNoNameOrCustomerIgnoringCase()
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
        Assert.Contains(byCode.Items, p => p.Id == created.Id);
    }

    [Fact]
    public async Task List_SearchByProjectNo_MatchesTheWholeNumberOnly()
    {
        // "14" finds Proje No 14, not 114 or 140. (Names may still contain the digits; those match by name.)
        var analyst = await NewUserAsync(UserRole.BusinessAnalyst);
        var created = await CreateAsync(analyst, Request(analyst, "Numara araması"));
        var longer = await InsertProjectAsync(analyst, "1", code: created.Code + "0", name: "Başka proje");

        var result = await ListAsync(new ProjectFilter { Search = created.Code, BusinessAnalystId = analyst });

        Assert.Equal([created.Id], result.Items.Select(p => p.Id));
        Assert.DoesNotContain(result.Items, p => p.Id == longer);
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
        // Not plain numbers, so the Proje No sequence of the other tests is not affected; the order rule is the same.
        var analyst = await NewUserAsync(UserRole.BusinessAnalyst);
        var prefix = $"L{Guid.NewGuid():N}"[..8];
        var code998 = await InsertProjectAsync(analyst, "1", code: $"{prefix}998");
        var code1000 = await InsertProjectAsync(analyst, "1", code: $"{prefix}1000");
        var code999 = await InsertProjectAsync(analyst, "1", code: $"{prefix}999");

        var result = await ListAsync(new ProjectFilter { BusinessAnalystId = analyst });

        Assert.Equal([code1000, code999, code998], result.Items.Select(p => p.Id));
    }

    [Fact]
    public async Task List_Paging_ReturnsThePageAndTheTotal()
    {
        var analyst = await NewUserAsync(UserRole.BusinessAnalyst);
        var prefix = $"P{Guid.NewGuid():N}"[..8];
        for (var i = 1; i <= 5; i++)
        {
            await InsertProjectAsync(analyst, "1", code: $"{prefix}{i:D3}");
        }

        var page2 = (await AsAsync(null, s => s.ListAsync(
            new ProjectFilter { BusinessAnalystId = analyst }, new PageRequest { Page = 2, PageSize = 2 },
            CancellationToken.None))).Result;

        Assert.Equal(5, page2.TotalCount);
        Assert.Equal([$"{prefix}003", $"{prefix}002"], page2.Items.Select(p => p.Code));
    }

    [Fact]
    public async Task List_Row_ShowsCurrentStepAndAnalyst()
    {
        var analyst = await NewUserAsync(UserRole.BusinessAnalyst, fullName: "Liste Analisti");
        await InsertProjectAsync(analyst, "12");

        var row = Assert.Single((await ListAsync(new ProjectFilter { BusinessAnalystId = analyst })).Items);

        Assert.Equal("12", row.CurrentStep?.Code);
        Assert.Equal(3, row.CurrentStep?.StageOrder);
        Assert.Equal(new UserSummaryDto(analyst, "Liste Analisti"), row.BusinessAnalyst);
    }
}
