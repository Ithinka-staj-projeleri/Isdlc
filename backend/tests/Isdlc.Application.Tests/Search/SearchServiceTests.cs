using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.Search;
using Isdlc.Application.Search.Dtos;
using Isdlc.Application.Tests.Common;
using Isdlc.Domain.Meetings;
using Isdlc.Domain.OpenIssues;
using Isdlc.Domain.Process;
using Isdlc.Domain.Projects;
using Isdlc.Domain.Requirements;
using Isdlc.Domain.Risks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Isdlc.Application.Tests.Search;

/// <summary>
/// Genel arama on a real database. Every test searches for its own random word, so seed data and other tests
/// never change the counts. Expectations: docs/search-api.md.
/// </summary>
[Collection("Database")]
public class SearchServiceTests(DatabaseFixture database)
{
    /// <summary>A word no other record contains.</summary>
    private static string NewWord() => $"zq{Guid.NewGuid():N}"[..10];

    private async Task<SearchResultDto> SearchAsync(string text)
    {
        await using var scope = database.CreateScope();
        var service = new SearchService(scope.ServiceProvider.GetRequiredService<IApplicationDbContext>());
        return await service.SearchAsync(text, CancellationToken.None);
    }

    /// <summary>Changes a test project's Proje No, name, parent or status.</summary>
    private async Task SetProjectAsync(
        int projectId, string? code = null, string? name = null, int? parentProjectId = null,
        ProjectStatus? status = null)
    {
        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var project = await db.Projects.SingleAsync(p => p.Id == projectId);
        project.Code = code ?? project.Code;
        project.Name = name ?? project.Name;
        project.ParentProjectId = parentProjectId ?? project.ParentProjectId;
        project.Status = status ?? project.Status;
        await db.SaveChangesAsync();
    }

    private async Task<int> AddOpenIssueAsync(
        int projectId, string title, string code = "AK-001", OpenIssueStatus status = OpenIssueStatus.Open)
    {
        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var issue = new OpenIssue
        {
            ProjectId = projectId, Code = code, Type = OpenIssueType.Question, Title = title, Status = status,
            OwnerName = "Hakan Yıldız",
            ClosedAt = status == OpenIssueStatus.Closed ? DateTime.UtcNow : null,
        };
        db.OpenIssues.Add(issue);
        await db.SaveChangesAsync();
        return issue.Id;
    }

    // ---- Search text ----

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("  b  ")]
    public async Task Search_ShorterThanTwoCharactersAfterTrim_ThrowsBusinessRule(string text)
    {
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => SearchAsync(text));
        Assert.Equal("En az 2 karakter girin.", error.Message);
    }

    [Fact]
    public async Task Search_LongerThan100Characters_ThrowsBusinessRule()
    {
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => SearchAsync(new string('x', 101)));
        Assert.Equal("En fazla 100 karakter olabilir.", error.Message);
    }

    [Fact]
    public async Task Search_NoMatch_ReturnsEveryGroupEmpty()
    {
        var result = await SearchAsync(NewWord());

        Assert.All(
            new[]
            {
                result.Projects.TotalCount, result.Improvements.TotalCount, result.OpenIssues.TotalCount,
                result.Meetings.TotalCount, result.Risks.TotalCount, result.Requirements.TotalCount,
            },
            count => Assert.Equal(0, count));
        Assert.Empty(result.Projects.Items);
    }

    // ---- Projects ----

    [Fact]
    public async Task Search_ProjectNamePart_FindsProjectIgnoringCase()
    {
        var word = NewWord();
        var projectId = await TestProjects.CreateAsync(database, "1");
        await SetProjectAsync(projectId, name: $"Bayi portalı {word}");

        var result = await SearchAsync($"  {word.ToUpperInvariant()} ");

        var project = Assert.Single(result.Projects.Items);
        Assert.Equal(projectId, project.Id);
        Assert.Equal(ProjectStatus.Active, project.Status);
        Assert.Equal(1, result.Projects.TotalCount);
    }

    [Fact]
    public async Task Search_ProjectCode_MatchesExactlyAndFindsTheAddOns()
    {
        var code = NewWord();
        var projectId = await TestProjects.CreateAsync(database, "1");
        await SetProjectAsync(projectId, code: code);

        var addOnId = await TestProjects.CreateAsync(database, null, flowCode: ProcessFlowCodes.AddOn);
        await SetProjectAsync(addOnId, code: $"{code} / EG-01", parentProjectId: projectId);

        // "14" does not find "114": a longer Proje No that only contains the text.
        var otherId = await TestProjects.CreateAsync(database, "1");
        await SetProjectAsync(otherId, code: $"{code}1");

        var result = await SearchAsync(code);

        Assert.Equal(new[] { addOnId, projectId }, result.Projects.Items.Select(p => p.Id));
    }

    [Fact]
    public async Task Search_ImprovementProposal_IsInItsOwnGroupOnly()
    {
        var word = NewWord();
        var proposalId = await TestProjects.CreateAsync(database, null, flowCode: ProcessFlowCodes.ProcessImprovement);
        await SetProjectAsync(proposalId, name: $"Toplantı notları {word}");

        var result = await SearchAsync(word);

        Assert.Equal(proposalId, Assert.Single(result.Improvements.Items).Id);
        Assert.Empty(result.Projects.Items);
    }

    // ---- Records inside projects ----

    [Fact]
    public async Task Search_RecordTitles_FindEachKindWithItsProject()
    {
        var word = NewWord();
        var projectId = await TestProjects.CreateAsync(database, "1");
        var project = await TestProjects.GetAsync(database, projectId);
        var analystId = await TestProjects.UserIdAsync(database, TestProjects.Analyst);

        var issueId = await AddOpenIssueAsync(projectId, $"Giriş ekranı onayı {word}");
        await using (var scope = database.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            db.Risks.Add(new Risk
            {
                ProjectId = projectId, Code = "RSK-001", Title = $"Servis dokümanı eksik {word}",
                Category = RiskCategory.Technical, Probability = 2, Impact = 2, OwnerUserId = analystId,
            });
            db.Requirements.Add(new Requirement
            {
                ProjectId = projectId, Code = "GR-001", Type = RequirementType.Functional,
                Title = $"Stok sayımı {word}", Priority = RequirementPriority.MustHave,
            });
            db.Meetings.Add(new Meeting
            {
                ProjectId = projectId, Code = "TT-001", Type = MeetingType.AnalysisSession,
                Date = new DateOnly(2026, 11, 20), RecordedById = analystId, Agenda = $"Kapsam görüşmesi {word}",
            });
            await db.SaveChangesAsync();
        }

        var result = await SearchAsync(word);

        var issue = Assert.Single(result.OpenIssues.Items);
        Assert.Equal(
            new SearchRecordDto(issueId, "AK-001", $"Giriş ekranı onayı {word}", "Open", projectId, project.Code, project.Name),
            issue);
        Assert.Equal("Open", Assert.Single(result.Risks.Items).Status);
        Assert.Equal("Draft", Assert.Single(result.Requirements.Items).Status);
        var meeting = Assert.Single(result.Meetings.Items);
        Assert.Equal($"Kapsam görüşmesi {word}", meeting.Title);
        Assert.Equal(project.Code, meeting.ProjectCode);
        Assert.Empty(result.Projects.Items);
    }

    [Fact]
    public async Task Search_RecordCode_FindsTheRecord()
    {
        var code = NewWord()[..8];
        var projectId = await TestProjects.CreateAsync(database, "1");
        var issueId = await AddOpenIssueAsync(projectId, "Kodla aranan açık konu", code: code);

        var result = await SearchAsync(code);

        Assert.Equal(issueId, Assert.Single(result.OpenIssues.Items).Id);
    }

    [Fact]
    public async Task Search_LongMeetingAgenda_TitleIsCutTo200Characters()
    {
        var word = NewWord();
        var projectId = await TestProjects.CreateAsync(database, "1");
        var analystId = await TestProjects.UserIdAsync(database, TestProjects.Analyst);
        await using (var scope = database.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            db.Meetings.Add(new Meeting
            {
                ProjectId = projectId, Code = "TT-001", Type = MeetingType.Internal,
                Date = new DateOnly(2026, 11, 20), RecordedById = analystId, Agenda = word + new string('g', 500),
            });
            await db.SaveChangesAsync();
        }

        var result = await SearchAsync(word);

        Assert.Equal(SearchService.MeetingTitleLength, Assert.Single(result.Meetings.Items).Title.Length);
    }

    // ---- Group size and order ----

    [Fact]
    public async Task Search_MoreThanFiveMatches_ReturnsFiveOpenOnesFirstAndTheTotal()
    {
        var word = NewWord();
        var projectId = await TestProjects.CreateAsync(database, "1");

        var openIds = new List<int>();
        for (var i = 1; i <= 5; i++)
        {
            openIds.Add(await AddOpenIssueAsync(projectId, $"{word} açık {i}", code: $"AK-00{i}"));
        }

        // Newer than every open one, but closed: they come last.
        await AddOpenIssueAsync(projectId, $"{word} kapalı 1", code: "AK-006", status: OpenIssueStatus.Closed);
        await AddOpenIssueAsync(projectId, $"{word} kapalı 2", code: "AK-007", status: OpenIssueStatus.Closed);

        var result = await SearchAsync(word);

        Assert.Equal(7, result.OpenIssues.TotalCount);
        Assert.Equal(openIds.AsEnumerable().Reverse(), result.OpenIssues.Items.Select(x => x.Id));
    }

    [Fact]
    public async Task Search_ClosedProject_IsFoundAfterTheActiveOnes()
    {
        var word = NewWord();
        var closedId = await TestProjects.CreateAsync(database, null, ProjectStatus.Closed);
        await SetProjectAsync(closedId, name: $"Eski proje {word}");
        var activeId = await TestProjects.CreateAsync(database, "1");
        await SetProjectAsync(activeId, name: $"Yeni proje {word}");
        var postponedId = await TestProjects.CreateAsync(database, null, ProjectStatus.Postponed);
        await SetProjectAsync(postponedId, name: $"Ertelenen proje {word}");

        var result = await SearchAsync(word);

        Assert.Equal(new[] { postponedId, activeId, closedId }, result.Projects.Items.Select(p => p.Id));
        Assert.Equal(ProjectStatus.Closed, result.Projects.Items[^1].Status);
    }
}
