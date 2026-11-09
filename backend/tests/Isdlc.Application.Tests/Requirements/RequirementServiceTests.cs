using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Common.Models;
using Isdlc.Application.Requirements;
using Isdlc.Application.Requirements.Dtos;
using Isdlc.Application.Tests.Common;
using Isdlc.Domain.Audit;
using Isdlc.Domain.Projects;
using Isdlc.Domain.Requirements;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Isdlc.Application.Tests.Requirements;

/// <summary>
/// Requirements on a real database. Every test creates its own project (TestProjects),
/// so seed projects are never changed. Expectations: docs/requirements-documents-api.md
/// </summary>
[Collection("Database")]
public class RequirementServiceTests(DatabaseFixture database)
{
    private static readonly PageRequest FirstPage = new() { Page = 1, PageSize = 100 };

    private async Task<T> AsAsync<T>(string? email, Func<IRequirementService, Task<T>> action)
    {
        var userId = email is null ? (int?)null : await TestProjects.UserIdAsync(database, email);
        await using var scope = database.CreateScope();
        var service = new RequirementService(
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(), new TestUser(userId));
        return await action(service);
    }

    private Task<RequirementDto> CreateAsync(int projectId, string title = "Mobil cihazdan iş emri kapatma") =>
        AsAsync(TestProjects.Developer, s => s.CreateAsync(projectId, NewRequirement(title), CancellationToken.None));

    private static CreateRequirementDto NewRequirement(string title) => new()
    {
        Type = RequirementType.Functional,
        Title = title,
        Description = "  Depo sorumlusu olarak iş emrini sahadan kapatmak istiyorum.  ",
        AcceptanceCriteria = "   ",
        Priority = RequirementPriority.MustHave,
    };

    private Task<RequirementDto> SetStatusAsync(int projectId, int requirementId, RequirementStatus status) =>
        AsAsync(TestProjects.Analyst, s => s.UpdateStatusAsync(
            projectId, requirementId, new UpdateRequirementStatusDto { Status = status }, CancellationToken.None));

    // ---- Create ----

    [Fact]
    public async Task Create_FirstRecord_StartsAsDraftWithFirstCode()
    {
        var projectId = await TestProjects.CreateAsync(database, "4.1");

        var created = await CreateAsync(projectId);

        Assert.Equal("GR-001", created.Code);
        Assert.Equal(RequirementStatus.Draft, created.Status);
        Assert.Equal(projectId, created.ProjectId);
        Assert.Equal("Depo sorumlusu olarak iş emrini sahadan kapatmak istiyorum.", created.Description);
        Assert.Null(created.AcceptanceCriteria); // blank text is not kept
    }

    [Fact]
    public async Task Create_NextRecords_GetIncreasingCodesPerProject()
    {
        var projectId = await TestProjects.CreateAsync(database, "4.1");
        var otherProjectId = await TestProjects.CreateAsync(database, "4.1");

        await CreateAsync(projectId);
        var second = await CreateAsync(projectId);
        var otherFirst = await CreateAsync(otherProjectId);

        Assert.Equal("GR-002", second.Code);
        Assert.Equal("GR-001", otherFirst.Code);
    }

    [Fact]
    public async Task Create_ProjectNotActive_ThrowsConflict()
    {
        var projectId = await TestProjects.CreateAsync(database, null, ProjectStatus.Lost);

        await Assert.ThrowsAsync<ConflictException>(() => CreateAsync(projectId));
    }

    [Fact]
    public async Task Create_ProjectMissing_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => CreateAsync(int.MaxValue));
    }

    [Fact]
    public async Task Create_NotSignedIn_ThrowsForbidden()
    {
        var projectId = await TestProjects.CreateAsync(database, "4.1");

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            AsAsync(null, s => s.CreateAsync(projectId, NewRequirement("Başlık"), CancellationToken.None)));
    }

    [Fact]
    public async Task Create_BlankTitle_ThrowsBusinessRule()
    {
        var projectId = await TestProjects.CreateAsync(database, "4.1");

        await Assert.ThrowsAsync<BusinessRuleException>(() => CreateAsync(projectId, "   "));
    }

    [Fact]
    public async Task Create_WritesAuditLogWithProject()
    {
        var projectId = await TestProjects.CreateAsync(database, "4.1");

        var created = await CreateAsync(projectId);

        await using var scope = database.CreateScope();
        var log = await scope.ServiceProvider.GetRequiredService<IApplicationDbContext>().AuditLogs
            .AsNoTracking()
            .SingleAsync(l => l.EntityName == nameof(Requirement) && l.EntityId == created.Id);
        Assert.Equal(projectId, log.ProjectId);
        Assert.Equal(AuditAction.Created, log.Action);
    }

    // ---- Update ----

    [Fact]
    public async Task Update_ChangesContent_KeepsCodeAndStatus()
    {
        var projectId = await TestProjects.CreateAsync(database, "4.1");
        var created = await CreateAsync(projectId);
        await SetStatusAsync(projectId, created.Id, RequirementStatus.InReview);

        var updated = await AsAsync(TestProjects.ProjectManager, s => s.UpdateAsync(projectId, created.Id, new UpdateRequirementDto
        {
            Type = RequirementType.NonFunctional,
            Title = "  İş emri 2 saniyede açılmalı  ",
            Description = null,
            AcceptanceCriteria = "Ortalama açılış süresi 2 saniyenin altında.",
            Priority = RequirementPriority.ShouldHave,
        }, CancellationToken.None));

        Assert.Equal(created.Code, updated.Code);
        Assert.Equal(RequirementStatus.InReview, updated.Status);
        Assert.Equal(RequirementType.NonFunctional, updated.Type);
        Assert.Equal("İş emri 2 saniyede açılmalı", updated.Title);
        Assert.Null(updated.Description);
        Assert.Equal(RequirementPriority.ShouldHave, updated.Priority);
        Assert.NotNull(updated.UpdatedAt);
    }

    [Fact]
    public async Task Update_RecordOfAnotherProject_ThrowsNotFound()
    {
        var projectId = await TestProjects.CreateAsync(database, "4.1");
        var otherProjectId = await TestProjects.CreateAsync(database, "4.1");
        var created = await CreateAsync(projectId);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            AsAsync(TestProjects.Analyst, s => s.UpdateAsync(
                otherProjectId, created.Id, new UpdateRequirementDto
                {
                    Type = RequirementType.Business, Title = "Başka proje", Priority = RequirementPriority.CouldHave,
                }, CancellationToken.None)));
    }

    // ---- Status ----

    [Fact]
    public async Task UpdateStatus_Cancelled_CanBeMadeDraftAgain()
    {
        var projectId = await TestProjects.CreateAsync(database, "4.1");
        var created = await CreateAsync(projectId);

        var cancelled = await SetStatusAsync(projectId, created.Id, RequirementStatus.Cancelled);
        var draft = await SetStatusAsync(projectId, created.Id, RequirementStatus.Draft);

        Assert.Equal(RequirementStatus.Cancelled, cancelled.Status);
        Assert.Equal(RequirementStatus.Draft, draft.Status);
    }

    // ---- List ----

    [Fact]
    public async Task List_CancelledLast_ThenByCode()
    {
        var projectId = await TestProjects.CreateAsync(database, "4.1");
        var first = await CreateAsync(projectId, "Birinci");
        await CreateAsync(projectId, "İkinci");
        await CreateAsync(projectId, "Üçüncü");
        await SetStatusAsync(projectId, first.Id, RequirementStatus.Cancelled);

        var result = await AsAsync(TestProjects.Sales, s =>
            s.ListAsync(projectId, new RequirementFilter(), FirstPage, CancellationToken.None));

        Assert.Equal(3, result.TotalCount);
        Assert.Equal(["GR-002", "GR-003", "GR-001"], result.Items.Select(x => x.Code));
    }

    [Fact]
    public async Task List_Filters_ReturnOnlyMatchingRecords()
    {
        var projectId = await TestProjects.CreateAsync(database, "4.1");
        var mobile = await CreateAsync(projectId, "Mobil onay ekranı");
        await CreateAsync(projectId, "Rapor çıktısı");
        await SetStatusAsync(projectId, mobile.Id, RequirementStatus.Approved);

        var bySearch = await AsAsync(TestProjects.Analyst, s =>
            s.ListAsync(projectId, new RequirementFilter { Search = "mobil" }, FirstPage, CancellationToken.None));
        var byStatus = await AsAsync(TestProjects.Analyst, s =>
            s.ListAsync(projectId, new RequirementFilter { Status = RequirementStatus.Draft }, FirstPage, CancellationToken.None));

        Assert.Equal([mobile.Code], bySearch.Items.Select(x => x.Code));
        Assert.Equal(["GR-002"], byStatus.Items.Select(x => x.Code));
    }

    [Fact]
    public async Task List_ClosedProject_CanStillBeRead()
    {
        var projectId = await TestProjects.CreateAsync(database, null, ProjectStatus.Closed);

        var result = await AsAsync(TestProjects.Analyst, s =>
            s.ListAsync(projectId, new RequirementFilter(), FirstPage, CancellationToken.None));

        Assert.Empty(result.Items);
    }
}
