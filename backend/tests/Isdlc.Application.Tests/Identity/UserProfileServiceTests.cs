using System.ComponentModel.DataAnnotations;
using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Dashboard;
using Isdlc.Application.Dashboard.Dtos;
using Isdlc.Application.Identity;
using Isdlc.Application.Identity.Dtos;
using Isdlc.Application.Tests.Common;
using Isdlc.Domain.Identity;
using Isdlc.Domain.OpenIssues;
using Isdlc.Domain.Process;
using Isdlc.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Isdlc.Application.Tests.Identity;

/// <summary>
/// Kişi profili and the owner selector (Faz 5 · F9) on a real database. Each test uses its own users and projects,
/// so seed users are never changed. Expectations: docs/users-api.md
/// </summary>
[Collection("Database")]
public class UserProfileServiceTests(DatabaseFixture database)
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private async Task<T> WithServiceAsync<T>(int? signedInUserId, Func<UserProfileService, Task<T>> action)
    {
        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var service = new UserProfileService(
            db, new TestUser(signedInUserId), TimeProvider.System, new TeamWorkloadService(db, TimeProvider.System));
        return await action(service);
    }

    private Task<UserProfileDto> GetAsync(int userId) =>
        WithServiceAsync(null, s => s.GetAsync(userId, CancellationToken.None));

    private async Task<TeamMemberWorkloadDto> WorkloadRowAsync(int userId)
    {
        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var rows = await new TeamWorkloadService(db, TimeProvider.System).GetAsync(CancellationToken.None);
        return rows.Single(r => r.User.Id == userId);
    }

    /// <summary>A new active project at the step, with the given analyst (null: nobody assigned).</summary>
    private async Task<int> NewProjectAsync(string stepCode, int? analystId, ProjectStatus status = ProjectStatus.Active)
    {
        var projectId = await TestProjects.CreateAsync(database, stepCode, status);
        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var project = await db.Projects.SingleAsync(p => p.Id == projectId);
        project.BusinessAnalystId = analystId;
        await db.SaveChangesAsync();
        return projectId;
    }

    private async Task<int> AddOpenIssueAsync(
        int projectId, string code, int ownerUserId, DateOnly? dueDate, OpenIssueStatus status = OpenIssueStatus.Open)
    {
        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var issue = new OpenIssue
        {
            ProjectId = projectId,
            Code = code,
            Type = OpenIssueType.Action,
            Title = $"Konu {code}",
            OwnerUserId = ownerUserId,
            DueDate = dueDate,
            Status = status,
            ClosedAt = status == OpenIssueStatus.Closed ? DateTime.UtcNow : null,
        };
        db.OpenIssues.Add(issue);
        await db.SaveChangesAsync();
        return issue.Id;
    }

    private async Task WaitingSinceLongAgoAsync(int projectId)
    {
        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        db.StepRecords.Add(new StepRecord
        {
            ProjectId = projectId,
            ProcessStepId = await TestProjects.StepIdAsync(database, "4"),
            Action = StepAction.Completed,
            PerformedById = await TestProjects.UserIdAsync(database, TestProjects.Analyst),
            PerformedAt = new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        });
        await db.SaveChangesAsync();
    }

    private async Task SetProfileAsync(int userId, string? expertise, string? note)
    {
        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var user = await db.Users.SingleAsync(u => u.Id == userId);
        user.Expertise = expertise;
        user.WorkingNote = note;
        await db.SaveChangesAsync();
    }

    // ---- Get ----

    [Fact]
    public async Task Get_UnknownUser_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => GetAsync(int.MaxValue));
    }

    [Fact]
    public async Task Get_ReturnsUserFieldsAndProfile()
    {
        var user = await TestUsers.CreateAsync(database, password: null, role: UserRole.TestSpecialist);
        await SetProfileAsync(user.Id, "Performans testi, Otomasyon", "Sabahları toplantıya girmiyorum.");

        var profile = await GetAsync(user.Id);

        Assert.Equal(user.FullName, profile.FullName);
        Assert.Equal(user.Email, profile.Email);
        Assert.Equal(UserRole.TestSpecialist, profile.Role);
        Assert.True(profile.IsActive);
        Assert.Equal("Performans testi, Otomasyon", profile.Expertise);
        Assert.Equal("Sabahları toplantıya girmiyorum.", profile.WorkingNote);
    }

    [Fact]
    public async Task Get_OpenIssues_OnlyNotClosedOnActiveProjects_OverdueFirst()
    {
        var owner = await TestUsers.CreateAsync(database, password: null);
        var active = await NewProjectAsync("6", analystId: null);
        var closedProject = await NewProjectAsync("6", analystId: null, ProjectStatus.Closed);

        var later = await AddOpenIssueAsync(active, "AK-001", owner.Id, Today.AddDays(5));
        var overdue = await AddOpenIssueAsync(active, "AK-002", owner.Id, Today.AddDays(-2));
        await AddOpenIssueAsync(active, "AK-003", owner.Id, null, OpenIssueStatus.Closed);
        await AddOpenIssueAsync(closedProject, "AK-001", owner.Id, null);

        var profile = await GetAsync(owner.Id);

        Assert.Equal(2, profile.OpenIssues.TotalCount);
        Assert.Equal([overdue, later], profile.OpenIssues.Items.Select(i => i.Id));
        Assert.True(profile.OpenIssues.Items[0].IsOverdue);
    }

    [Fact]
    public async Task Get_ActiveUser_WorkloadIsTheTeamWorkloadRow()
    {
        var analyst = await TestUsers.CreateAsync(database, password: null, role: UserRole.BusinessAnalyst);
        var projectId = await NewProjectAsync("6", analyst.Id);
        await AddOpenIssueAsync(projectId, "AK-001", analyst.Id, Today.AddDays(-1));
        await AddOpenIssueAsync(projectId, "AK-002", analyst.Id, null);

        var profile = await GetAsync(analyst.Id);
        var row = await WorkloadRowAsync(analyst.Id);

        Assert.Equal(new UserWorkloadCountsDto(row.OpenIssues, row.OverdueOpenIssues, row.WaitingSteps), profile.Workload);
        Assert.Equal(2, profile.Workload!.OpenIssues);
        Assert.Equal(1, profile.Workload.OverdueOpenIssues);
        // The lists' totals are the same numbers (P5).
        Assert.Equal(row.OpenIssues, profile.OpenIssues.TotalCount);
        Assert.Equal(row.WaitingSteps, profile.WaitingSteps.TotalCount);
    }

    [Fact]
    public async Task Get_WaitingSteps_AnalystSeesTheirProjectNotAnotherAnalysts()
    {
        var analyst = await TestUsers.CreateAsync(database, password: null, role: UserRole.BusinessAnalyst);
        var other = await TestUsers.CreateAsync(database, password: null, role: UserRole.BusinessAnalyst);
        var mine = await NewProjectAsync("6", analyst.Id);
        var theirs = await NewProjectAsync("6", other.Id);
        // Unassigned analyst steps of other tests count for every analyst too (F2-3): waiting the longest puts
        // this project at the top of the list (at most 10 rows).
        await WaitingSinceLongAgoAsync(mine);

        var profile = await GetAsync(analyst.Id);

        var item = Assert.Single(profile.WaitingSteps.Items, x => x.Project.Id == mine);
        Assert.Equal("6", item.Step.Code);
        Assert.DoesNotContain(profile.WaitingSteps.Items, x => x.Project.Id == theirs);
        Assert.DoesNotContain((await GetAsync(other.Id)).WaitingSteps.Items, x => x.Project.Id == mine);
    }

    [Fact]
    public async Task Get_InactiveUser_HasNoWorkloadOrWaitingSteps_ButKeepsOpenIssues()
    {
        var user = await TestUsers.CreateAsync(database, password: null, role: UserRole.SoftwareDepartmentManager);
        var projectId = await NewProjectAsync("6", analystId: null);
        var issueId = await AddOpenIssueAsync(projectId, "AK-001", user.Id, null);
        await using (var scope = database.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            (await db.Users.SingleAsync(u => u.Id == user.Id)).IsActive = false;
            await db.SaveChangesAsync();
        }

        var profile = await GetAsync(user.Id);

        Assert.False(profile.IsActive);
        Assert.Null(profile.Workload);
        Assert.Equal(0, profile.WaitingSteps.TotalCount);
        Assert.Empty(profile.WaitingSteps.Items);
        Assert.Equal(issueId, Assert.Single(profile.OpenIssues.Items).Id);
    }

    // ---- Update mine ----

    [Fact]
    public async Task UpdateMine_SavesTrimmedValues_AndEmptyAsNull()
    {
        var user = await TestUsers.CreateAsync(database, password: null);
        await SetProfileAsync(user.Id, "Eski", "Eski not");

        var profile = await WithServiceAsync(user.Id, s => s.UpdateMineAsync(
            new UpdateMyProfileDto { Expertise = "  SQL, Raporlama  ", WorkingNote = "   " }, CancellationToken.None));

        Assert.Equal("SQL, Raporlama", profile.Expertise);
        Assert.Null(profile.WorkingNote);
        var saved = await GetAsync(user.Id);
        Assert.Equal("SQL, Raporlama", saved.Expertise);
        Assert.Null(saved.WorkingNote);
    }

    [Fact]
    public async Task UpdateMine_ChangesOnlyTheSignedInUser()
    {
        var me = await TestUsers.CreateAsync(database, password: null);
        var other = await TestUsers.CreateAsync(database, password: null);
        await SetProfileAsync(other.Id, "Başkasının uzmanlığı", null);

        await WithServiceAsync(me.Id, s => s.UpdateMineAsync(
            new UpdateMyProfileDto { Expertise = "Benim" }, CancellationToken.None));

        Assert.Equal("Benim", (await GetAsync(me.Id)).Expertise);
        Assert.Equal("Başkasının uzmanlığı", (await GetAsync(other.Id)).Expertise);
    }

    [Fact]
    public async Task UpdateMine_NotSignedIn_ThrowsForbidden()
    {
        await Assert.ThrowsAsync<ForbiddenException>(() => WithServiceAsync(null, s => s.UpdateMineAsync(
            new UpdateMyProfileDto { Expertise = "SQL" }, CancellationToken.None)));
    }

    [Fact]
    public void UpdateDto_TooLongValues_ReturnsErrorOnEachField()
    {
        var dto = new UpdateMyProfileDto
        {
            Expertise = new string('a', UserProfileLimits.Expertise + 1),
            WorkingNote = new string('a', UserProfileLimits.WorkingNote + 1),
        };
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);

        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(UpdateMyProfileDto.Expertise)));
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(UpdateMyProfileDto.WorkingNote)));
    }

    // ---- Owner selector ----

    [Fact]
    public async Task ListWorkload_ActiveUsersOnly_ByName_WithExpertiseAndTheWorkloadNumbers()
    {
        var owner = await TestUsers.CreateAsync(database, password: null);
        var inactive = await TestUsers.CreateAsync(database, password: null, isActive: false);
        await SetProfileAsync(owner.Id, "Entegrasyon", null);
        var projectId = await NewProjectAsync("6", analystId: null);
        await AddOpenIssueAsync(projectId, "AK-001", owner.Id, Today.AddDays(-3));

        var list = await WithServiceAsync(null, s => s.ListWorkloadAsync(CancellationToken.None));

        Assert.DoesNotContain(list, u => u.Id == inactive.Id);
        var item = Assert.Single(list, u => u.Id == owner.Id);
        Assert.Equal("Entegrasyon", item.Expertise);
        Assert.Equal(1, item.OpenIssues);
        Assert.Equal(1, item.OverdueOpenIssues);

        var names = list.Select(u => u.FullName).ToList();
        Assert.Equal(names.OrderBy(n => n, StringComparer.Create(new("tr-TR"), ignoreCase: true)), names);
    }
}
