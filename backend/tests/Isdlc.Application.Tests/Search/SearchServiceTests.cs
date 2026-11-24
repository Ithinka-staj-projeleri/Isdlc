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

    // ---- Cross tests (PLANNING.md): Erva → Baran's modules ----

    private async Task<SearchResultDto> SearchAsCompanyAsync(int tenantId, string text)
    {
        await using var scope = database.CreateScope(tenantId);
        var service = new SearchService(scope.ServiceProvider.GetRequiredService<IApplicationDbContext>());
        return await service.SearchAsync(text, CancellationToken.None);
    }

    private static void AssertEmpty(SearchResultDto result)
    {
        Assert.Empty(result.Projects.Items);
        Assert.Empty(result.Improvements.Items);
        Assert.Empty(result.OpenIssues.Items);
        Assert.Empty(result.Meetings.Items);
        Assert.Empty(result.Risks.Items);
        Assert.Empty(result.Requirements.Items);
    }

    [Fact]
    public async Task Search_WordOnlyInLongTexts_IsNotFound()
    {
        // S1: descriptions and notes are not searched; only codes, titles, the customer and the agenda.
        var word = NewWord();
        var projectId = await TestProjects.CreateAsync(database, "1");
        await using (var scope = database.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var project = await db.Projects.SingleAsync(p => p.Id == projectId);
            project.Description = $"Uzun açıklama {word}";
            project.Notes = $"Not {word}";
            project.RequesterName = $"İleten {word}";
            db.OpenIssues.Add(new OpenIssue
            {
                ProjectId = projectId, Code = "AK-001", Type = OpenIssueType.Question, Title = "Başlıkta yok",
                Description = $"Açıklamada var {word}", OwnerName = "Hakan Yıldız",
            });
            await db.SaveChangesAsync();
        }

        AssertEmpty(await SearchAsync(word));
    }

    [Fact]
    public async Task Search_CustomerName_FindsTheProject()
    {
        var word = NewWord();
        var projectId = await TestProjects.CreateAsync(database, "1");
        await using (var scope = database.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            (await db.Projects.SingleAsync(p => p.Id == projectId)).CustomerName = $"Örnek Lojistik {word}";
            await db.SaveChangesAsync();
        }

        var result = await SearchAsync(word);

        Assert.Equal($"Örnek Lojistik {word}", Assert.Single(result.Projects.Items).CustomerName);
    }

    [Fact]
    public async Task Search_TwoAndHundredCharacters_AreAccepted()
    {
        // S3: the limits themselves are allowed.
        await SearchAsync("zq");
        await SearchAsync(new string('z', SearchService.MaxLength));
    }

    [Fact]
    public async Task Search_ImprovementCode_FindsTheProposal()
    {
        var word = NewWord();
        var proposalId = await TestProjects.CreateAsync(database, null, flowCode: ProcessFlowCodes.ProcessImprovement);
        await SetProjectAsync(proposalId, code: $"SI-{word}");

        var result = await SearchAsync($"SI-{word}");

        Assert.Equal(proposalId, Assert.Single(result.Improvements.Items).Id);
    }

    [Fact]
    public async Task Search_RisksAndRequirements_ClosedOrCancelledComeLast()
    {
        // S5: a newer closed risk and a newer cancelled requirement come after the older open ones.
        var word = NewWord();
        var projectId = await TestProjects.CreateAsync(database, "1");
        var analystId = await TestProjects.UserIdAsync(database, TestProjects.Analyst);
        int openRiskId, closedRiskId, openRequirementId, cancelledRequirementId;
        await using (var scope = database.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            Risk NewRisk(string code, RiskStatus status) => new()
            {
                ProjectId = projectId, Code = code, Title = $"{word} {code}", Category = RiskCategory.Technical,
                Probability = 2, Impact = 2, OwnerUserId = analystId, Status = status,
            };
            Requirement NewRequirement(string code, RequirementStatus status) => new()
            {
                ProjectId = projectId, Code = code, Type = RequirementType.Functional, Title = $"{word} {code}",
                Priority = RequirementPriority.MustHave, Status = status,
            };

            var openRisk = NewRisk("RSK-001", RiskStatus.Monitoring);
            var openRequirement = NewRequirement("GR-001", RequirementStatus.Approved);
            db.Risks.Add(openRisk);
            db.Requirements.Add(openRequirement);
            await db.SaveChangesAsync();
            var closedRisk = NewRisk("RSK-002", RiskStatus.Closed);
            var cancelledRequirement = NewRequirement("GR-002", RequirementStatus.Cancelled);
            db.Risks.Add(closedRisk);
            db.Requirements.Add(cancelledRequirement);
            await db.SaveChangesAsync();
            (openRiskId, closedRiskId) = (openRisk.Id, closedRisk.Id);
            (openRequirementId, cancelledRequirementId) = (openRequirement.Id, cancelledRequirement.Id);
        }

        var result = await SearchAsync(word);

        Assert.Equal(new[] { openRiskId, closedRiskId }, result.Risks.Items.Select(x => x.Id));
        Assert.Equal(new[] { openRequirementId, cancelledRequirementId }, result.Requirements.Items.Select(x => x.Id));
        Assert.Equal("Cancelled", result.Requirements.Items[^1].Status);
    }

    [Fact]
    public async Task Search_Meetings_LatestMeetingDateFirstNotTheLatestRecord()
    {
        var word = NewWord();
        var projectId = await TestProjects.CreateAsync(database, "1");
        var analystId = await TestProjects.UserIdAsync(database, TestProjects.Analyst);
        int laterMeetingId, earlierMeetingId;
        await using (var scope = database.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var later = new Meeting
            {
                ProjectId = projectId, Code = "TT-001", Type = MeetingType.Internal,
                Date = new DateOnly(2026, 11, 20), RecordedById = analystId, Agenda = $"{word} geç toplantı",
            };
            db.Meetings.Add(later);
            await db.SaveChangesAsync();
            // Written down later, but held earlier.
            var earlier = new Meeting
            {
                ProjectId = projectId, Code = "TT-002", Type = MeetingType.Internal,
                Date = new DateOnly(2026, 11, 2), RecordedById = analystId, Agenda = $"{word} erken toplantı",
            };
            db.Meetings.Add(earlier);
            await db.SaveChangesAsync();
            (laterMeetingId, earlierMeetingId) = (later.Id, earlier.Id);
        }

        var result = await SearchAsync(word);

        Assert.Equal(new[] { laterMeetingId, earlierMeetingId }, result.Meetings.Items.Select(m => m.Id));
    }

    [Fact]
    public async Task Search_AnotherCompanysRecords_AreNotFound()
    {
        // Faz 6: Deneme's project and open issue are found only by Deneme.
        var word = NewWord();
        var projectId = await TestProjects.CreateAsync(database, "5", tenantId: database.DenemeId);
        await using (var scope = database.CreateScope(database.DenemeId))
        {
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            (await db.Projects.SingleAsync(p => p.Id == projectId)).Name = $"Deneme projesi {word}";
            db.OpenIssues.Add(new OpenIssue
            {
                ProjectId = projectId, Code = "AK-001", Type = OpenIssueType.Question, Title = $"Deneme sorusu {word}",
                OwnerName = "Hakan Yıldız",
            });
            await db.SaveChangesAsync();
        }

        AssertEmpty(await SearchAsync(word));

        var deneme = await SearchAsCompanyAsync(database.DenemeId, word);
        Assert.Equal(projectId, Assert.Single(deneme.Projects.Items).Id);
        Assert.Equal(projectId, Assert.Single(deneme.OpenIssues.Items).ProjectId);
    }
}
