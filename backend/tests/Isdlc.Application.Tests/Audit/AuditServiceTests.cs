using Isdlc.Application.Audit;
using Isdlc.Application.Audit.Dtos;
using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Common.Models;
using Isdlc.Application.Documents;
using Isdlc.Application.Documents.Dtos;
using Isdlc.Application.Meetings;
using Isdlc.Application.Meetings.Dtos;
using Isdlc.Application.Requirements;
using Isdlc.Application.Requirements.Dtos;
using Isdlc.Application.Tests.Common;
using Isdlc.Domain.Assessment;
using Isdlc.Domain.Audit;
using Isdlc.Domain.Documents;
using Isdlc.Domain.Meetings;
using Isdlc.Domain.Requirements;
using Isdlc.Domain.Risks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Isdlc.Application.Tests.Audit;

/// <summary>
/// The Kayıt Geçmişi of a project on a real database. Changes are made through the real services, so these tests
/// also cover AuditLogInterceptor, which writes the log rows. Every test creates its own project (TestProjects).
/// In this fixture the signed-in user for saving is Elif Yılmaz. Expectations: docs/audit-api.md.
/// Cross test (PLANNING.md): Dide → Erva's modules.
/// </summary>
[Collection("Database")]
public class AuditServiceTests(DatabaseFixture database)
{
    private static readonly PageRequest FirstPage = new() { Page = 1, PageSize = 100 };

    private async Task<PagedResult<AuditLogDto>> ListAsync(
        int projectId, string? entityType = null, PageRequest? pageRequest = null)
    {
        await using var scope = database.CreateScope();
        var service = new AuditService(scope.ServiceProvider.GetRequiredService<IApplicationDbContext>());
        return await service.ListProjectLogAsync(
            projectId, new AuditLogFilter { EntityType = entityType }, pageRequest ?? FirstPage, CancellationToken.None);
    }

    /// <summary>Runs a change with a fresh scope, like one HTTP request.</summary>
    private async Task<T> InScopeAsync<T>(Func<IApplicationDbContext, int, Task<T>> action)
    {
        var analystId = await TestProjects.UserIdAsync(database, TestProjects.Analyst);
        await using var scope = database.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(), analystId);
    }

    private Task<int> InScopeAsync(Func<IApplicationDbContext, Task> action) =>
        InScopeAsync(async (db, _) => { await action(db); return 0; });

    private static string? NewValueOf(AuditLogDto log, string field) =>
        Assert.Single(log.Changes, c => c.Field == field).NewValue;

    // ---- Basics ----

    [Fact]
    public async Task List_ProjectMissing_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => ListAsync(int.MaxValue));
    }

    [Fact]
    public async Task List_CreatedThenUpdated_NewestFirstWithFieldChanges()
    {
        var projectId = await TestProjects.CreateAsync(database, "4.1");
        var requirement = await InScopeAsync((db, userId) =>
            new RequirementService(db, new TestUser(userId)).CreateAsync(projectId, new CreateRequirementDto
            {
                Type = RequirementType.Functional, Title = "Mobil onay", Priority = RequirementPriority.MustHave,
            }, CancellationToken.None));
        await InScopeAsync((db, userId) =>
            new RequirementService(db, new TestUser(userId)).UpdateStatusAsync(projectId, requirement.Id,
                new UpdateRequirementStatusDto { Status = RequirementStatus.Approved }, CancellationToken.None));

        var result = await ListAsync(projectId, "Requirement");

        Assert.Equal(2, result.TotalCount);
        var (updated, created) = (result.Items[0], result.Items[1]);

        Assert.Equal(AuditAction.Updated, updated.Action);
        Assert.Equal("GR-001", updated.RecordCode);
        Assert.Equal("Requirement", updated.EntityType);
        Assert.Equal(requirement.Id, updated.EntityId);
        Assert.Equal(new AuditFieldChangeDto("Status", "Draft", "Approved"), Assert.Single(updated.Changes));
        Assert.Equal("Elif Yılmaz", updated.PerformedBy.FullName);
        Assert.True(updated.PerformedAt >= created.PerformedAt);

        Assert.Equal(AuditAction.Created, created.Action);
        Assert.Equal(new AuditFieldChangeDto("Title", null, "Mobil onay"), Assert.Single(created.Changes, c => c.Field == "Title"));
        // Same on every row of a project's log, so it is never shown.
        Assert.DoesNotContain(result.Items.SelectMany(i => i.Changes), c => c.Field == "ProjectId");
    }

    // ---- References are shown readable ----

    [Fact]
    public async Task List_ProjectChange_ShowsPeopleAndStepCodesInsteadOfIds()
    {
        var projectId = await TestProjects.CreateAsync(database, "6.3");
        var managerId = await TestProjects.UserIdAsync(database, TestProjects.Manager);
        var stepId = await TestProjects.StepIdAsync(database, "7.1");
        await InScopeAsync(async db =>
        {
            var project = await db.Projects.SingleAsync(p => p.Id == projectId);
            project.ProjectManagerId = managerId;
            project.CurrentStepId = stepId;
            await db.SaveChangesAsync();
        });

        var update = (await ListAsync(projectId, "Project")).Items[0];

        Assert.Equal(AuditAction.Updated, update.Action);
        Assert.Equal((await TestProjects.GetAsync(database, projectId)).Code, update.RecordCode);
        Assert.Contains(new AuditFieldChangeDto("ProjectManagerId", "Ayşe Demir", "Mert Kaya"), update.Changes);
        Assert.Contains(new AuditFieldChangeDto("CurrentStepId", "6.3", "7.1"), update.Changes);
    }

    [Fact]
    public async Task List_MeetingAction_ShowsOwnerNameAndMeetingCode()
    {
        var projectId = await TestProjects.CreateAsync(database, "7.1");
        var developerId = await TestProjects.UserIdAsync(database, TestProjects.Developer);
        await InScopeAsync((db, userId) =>
            new MeetingService(db, new TestUser(userId), new RecordingNotificationService(), TimeProvider.System)
                .CreateAsync(projectId, new CreateMeetingDto
                {
                    Type = MeetingType.AnalysisSession,
                    Date = new DateOnly(2026, 11, 5),
                    Participants = "Elif Yılmaz, Can Öztürk",
                    Agenda = "Ekranlar",
                    Actions = [new MeetingActionDto { Title = "Ekran taslakları", OwnerUserId = developerId }],
                }, CancellationToken.None));

        var issue = Assert.Single((await ListAsync(projectId, "OpenIssue")).Items);

        Assert.Equal("AK-001", issue.RecordCode);
        Assert.Equal("Can Öztürk", NewValueOf(issue, "OwnerUserId"));
        // The meeting and its actions are saved together: the log must hold the meeting's real id, not a temporary one.
        Assert.Equal("TT-001", NewValueOf(issue, "SourceMeetingId"));

        var meeting = Assert.Single((await ListAsync(projectId, "Meeting")).Items);
        Assert.Equal("TT-001", meeting.RecordCode);
    }

    [Fact]
    public async Task List_DocumentChange_ShowsTheDocumentName()
    {
        var projectId = await TestProjects.CreateAsync(database, "4.1");
        await InScopeAsync(async (db, userId) =>
        {
            var typeId = await db.DocumentTypes.Where(t => t.Code == "PKD").Select(t => t.Id).SingleAsync();
            return await new DocumentService(db, new TestUser(userId)).UpdateAsync(projectId, typeId,
                new UpdateProjectDocumentDto { Status = DocumentStatus.InReview }, CancellationToken.None);
        });

        var log = Assert.Single((await ListAsync(projectId, "ProjectDocument")).Items);

        Assert.Equal("Proje Kapsam Dokümanı", log.RecordCode);
        Assert.Equal("Proje Kapsam Dokümanı", NewValueOf(log, "DocumentTypeId"));
        Assert.Equal("InReview", NewValueOf(log, "Status"));
    }

    [Fact]
    public async Task List_RiskHasItsCode_AssessmentHasNone()
    {
        var projectId = await TestProjects.CreateAsync(database, "4.8");
        var developerId = await TestProjects.UserIdAsync(database, TestProjects.Developer);
        await InScopeAsync(async db =>
        {
            db.Risks.Add(new Risk
            {
                ProjectId = projectId, Code = "RSK-001", Title = "ERP gecikmesi", Category = RiskCategory.Technical,
                Probability = 4, Impact = 5, OwnerUserId = developerId,
            });
            db.ProjectAssessments.Add(new ProjectAssessment { ProjectId = projectId, Effort = 2 });
            await db.SaveChangesAsync();
        });

        var risk = Assert.Single((await ListAsync(projectId, "Risk")).Items);
        var assessment = Assert.Single((await ListAsync(projectId, "ProjectAssessment")).Items);

        Assert.Equal("RSK-001", risk.RecordCode);
        Assert.Equal("Can Öztürk", NewValueOf(risk, "OwnerUserId"));
        // One assessment per project and no code of its own (docs/audit-api.md).
        Assert.Null(assessment.RecordCode);
        Assert.Equal("2", NewValueOf(assessment, "Effort"));
    }

    // ---- Filter, paging and project boundary ----

    [Fact]
    public async Task List_FilterPagingAndOtherProjects()
    {
        var projectId = await TestProjects.CreateAsync(database, "4.1");
        var otherProjectId = await TestProjects.CreateAsync(database, "4.1");
        foreach (var (id, title) in new[] { (projectId, "Birinci"), (projectId, "İkinci"), (otherProjectId, "Başka") })
        {
            await InScopeAsync((db, userId) => new RequirementService(db, new TestUser(userId)).CreateAsync(id,
                new CreateRequirementDto
                {
                    Type = RequirementType.Business, Title = title, Priority = RequirementPriority.CouldHave,
                }, CancellationToken.None));
        }

        var all = await ListAsync(projectId);
        var requirements = await ListAsync(projectId, "Requirement");
        var secondPage = await ListAsync(projectId, "Requirement", new PageRequest { Page = 2, PageSize = 1 });

        // The project's own "Created" row plus two requirements; the other project's requirement is not here.
        Assert.Equal(3, all.TotalCount);
        Assert.Equal(["GR-002", "GR-001"], requirements.Items.Select(x => x.RecordCode));
        Assert.Equal(2, secondPage.TotalCount);
        Assert.Equal("GR-001", Assert.Single(secondPage.Items).RecordCode);
        Assert.Single(all.Items, x => x.EntityType == "Project");
    }

    // ---- Old or damaged rows (written before a fix, or by hand) ----

    [Fact]
    public async Task List_OldOrDamagedRows_AreShownWithoutBreakingTheHistory()
    {
        var projectId = await TestProjects.CreateAsync(database, "6.3");
        var rowIds = await InScopeAsync(async (db, userId) =>
        {
            AuditLog Row(string changes, int entityId = int.MaxValue) => new()
            {
                EntityName = "OpenIssue",
                EntityId = entityId,
                ProjectId = projectId,
                Action = AuditAction.Updated,
                Changes = changes,
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
            };

            var unknownUser = Row("""{"OwnerUserId":{"old":2147483000,"new":null}}""");
            var temporaryId = Row("""{"SourceMeetingId":{"old":null,"new":-2147482647}}""");
            var broken = Row("{bozuk");
            db.AuditLogs.AddRange(unknownUser, temporaryId, broken);
            await db.SaveChangesAsync();
            return (UnknownUser: unknownUser.Id, TemporaryId: temporaryId.Id, Broken: broken.Id);
        });

        var items = (await ListAsync(projectId, "OpenIssue")).Items.ToDictionary(x => x.Id);

        // A record that cannot be found keeps its id, so nothing in the history is hidden.
        Assert.Equal(new AuditFieldChangeDto("OwnerUserId", "2147483000", null), Assert.Single(items[rowIds.UnknownUser].Changes));
        // EF's temporary negative id never pointed to a real record: shown as unknown.
        Assert.Equal(new AuditFieldChangeDto("SourceMeetingId", null, null), Assert.Single(items[rowIds.TemporaryId].Changes));
        // Broken JSON: the row is still listed, just without field changes.
        Assert.Empty(items[rowIds.Broken].Changes);
        // The record itself cannot be found: no code.
        Assert.All(items.Values, x => Assert.Null(x.RecordCode));
    }
}
