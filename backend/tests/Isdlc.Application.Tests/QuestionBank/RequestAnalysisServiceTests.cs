using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Common.Interfaces;
using Isdlc.Application.QuestionBank;
using Isdlc.Application.QuestionBank.Dtos;
using Isdlc.Application.Tests.Common;
using Isdlc.Domain.OpenIssues;
using Isdlc.Domain.Projects;
using Isdlc.Domain.QuestionBank;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Isdlc.Application.Tests.QuestionBank;

/// <summary>
/// Talep Analizi (E3) on a real database with the seeded "request-analysis" set: the open items and their transfer to
/// Açık Konular. Every test creates its own project (TestProjects: an internal request at step 6, "Talep anlaşılır").
/// Expectations: docs/question-bank-api.md → "Talep Analizi (E3)".
/// </summary>
[Collection("Database")]
public class RequestAnalysisServiceTests(DatabaseFixture database)
{
    private const string Set = RequestAnalysisRules.SetCode;

    private async Task<T> AsAsync<T>(Func<IQuestionnaireService, IRequestAnalysisService, Task<T>> action)
    {
        var userId = await TestProjects.UserIdAsync(database, TestProjects.Analyst);
        await using var scope = database.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var questionnaires = new QuestionnaireService(db, new TestUser(userId));
        return await action(questionnaires, new RequestAnalysisService(db, questionnaires, new TestUser(userId)));
    }

    private Task<QuestionnaireDto> FormAsync(int projectId) => AsAsync((q, _) => q.GetAsync(projectId, Set, default));

    /// <summary>Answers the form's question at the given position with the option of the given code.</summary>
    private async Task<QuestionDto> AnswerAsync(
        int projectId, int position, string optionCode, string? note = null, string? text = null)
    {
        var question = (await FormAsync(projectId)).Questions[position].Question;
        var optionId = question.Options.Single(o => o.Code == optionCode).Id;
        await AsAsync((q, _) => q.SaveAnswerAsync(
            projectId, Set, question.Id, new SaveAnswerDto { OptionId = optionId, Note = note, Text = text }, default));
        return question;
    }

    private Task<IReadOnlyList<RequestAnalysisOpenItemDto>> OpenItemsAsync(int projectId) =>
        AsAsync((_, r) => r.ListOpenItemsAsync(projectId, default));

    private Task<RequestAnalysisOpenItemDto> TransferAsync(int projectId, int questionId) =>
        AsAsync((_, r) => r.TransferToOpenIssueAsync(projectId, questionId, default));

    private async Task<OpenIssue> IssueAsync(int openIssueId)
    {
        await using var scope = database.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IApplicationDbContext>().OpenIssues
            .AsNoTracking()
            .SingleAsync(x => x.Id == openIssueId);
    }

    // ---- Open items ----

    [Fact]
    public async Task OpenItems_OnlyOpenAndPartlyAnswered_InFormOrder()
    {
        var projectId = await TestProjects.CreateAsync(database, "6");
        await AnswerAsync(projectId, 0, "answered");
        var partial = await AnswerAsync(projectId, 1, "partly-answered", note: "Bütçe bilgisi gelecek.");
        await AnswerAsync(projectId, 2, "not-applicable");
        var open = await AnswerAsync(projectId, 3, "open-issue", note: "Kullanıcı sayısı bilinmiyor.", text: "Birim müdürü dönecek.");

        var items = await OpenItemsAsync(projectId);

        Assert.Equal([partial.Id, open.Id], items.Select(i => i.QuestionId));
        var item = items[1];
        Assert.Equal(OptionMeaning.Open, item.Meaning);
        Assert.Equal("Açık Konu Var", item.OptionLabel);
        Assert.Equal("Kullanıcı sayısı bilinmiyor.", item.Note);
        Assert.Equal("Birim müdürü dönecek.", item.AnswerText);
        Assert.Null(item.OpenIssue);
    }

    [Fact]
    public async Task OpenItems_NothingAnswered_IsEmpty()
    {
        var projectId = await TestProjects.CreateAsync(database, "6");

        Assert.Empty(await OpenItemsAsync(projectId));
    }

    [Fact]
    public async Task OpenItems_UnknownProject_Throws404()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => OpenItemsAsync(int.MaxValue));
    }

    // ---- Transfer ----

    [Fact]
    public async Task Transfer_OpensAQuestionOwnedByTheRequesterWithTheAnswer()
    {
        var projectId = await TestProjects.CreateAsync(database, "6");
        var question = await AnswerAsync(projectId, 0, "open-issue", note: "Rapor örnekleri istendi.", text: "Örnekler gelecek hafta.");

        var item = await TransferAsync(projectId, question.Id);

        Assert.NotNull(item.OpenIssue);
        Assert.Equal("AK-001", item.OpenIssue.Code);
        Assert.Equal(OpenIssueStatus.Open, item.OpenIssue.Status);

        var issue = await IssueAsync(item.OpenIssue.Id);
        Assert.Equal(OpenIssueType.Question, issue.Type);
        Assert.Equal(question.Text, issue.Title);
        Assert.Equal("Test", issue.OwnerName); // the project's talebi ileten kişi
        Assert.Null(issue.OwnerUserId);
        Assert.Contains("Açık konu / eksik bilgi: Rapor örnekleri istendi.", issue.Description);
        Assert.Contains("Paydaş yanıtı: Örnekler gelecek hafta.", issue.Description);
        Assert.NotNull(issue.SourceQuestionAnswerId);
    }

    [Fact]
    public async Task Transfer_ShowsInTheOpenItems()
    {
        var projectId = await TestProjects.CreateAsync(database, "6");
        var question = await AnswerAsync(projectId, 0, "partly-answered");
        var transferred = await TransferAsync(projectId, question.Id);

        var item = Assert.Single(await OpenItemsAsync(projectId));

        Assert.Equal(transferred.OpenIssue, item.OpenIssue);
    }

    [Fact]
    public async Task Transfer_Twice_Throws409WithTheCode()
    {
        var projectId = await TestProjects.CreateAsync(database, "6");
        var question = await AnswerAsync(projectId, 0, "open-issue");
        await TransferAsync(projectId, question.Id);

        var error = await Assert.ThrowsAsync<ConflictException>(() => TransferAsync(projectId, question.Id));

        Assert.Equal("Bu soru zaten Açık Konular'a aktarıldı (AK-001).", error.Message);
    }

    [Fact]
    public async Task Transfer_SecondQuestion_GetsTheNextCode()
    {
        var projectId = await TestProjects.CreateAsync(database, "6");
        var first = await AnswerAsync(projectId, 0, "open-issue");
        var second = await AnswerAsync(projectId, 1, "open-issue");
        await TransferAsync(projectId, first.Id);

        var item = await TransferAsync(projectId, second.Id);

        Assert.Equal("AK-002", item.OpenIssue?.Code);
    }

    [Theory]
    [InlineData("answered")]
    [InlineData("not-applicable")]
    public async Task Transfer_AnswerNotOpen_Throws400(string optionCode)
    {
        var projectId = await TestProjects.CreateAsync(database, "6");
        var question = await AnswerAsync(projectId, 0, optionCode);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => TransferAsync(projectId, question.Id));

        Assert.Equal(
            "Yalnızca \"Açık Konu Var\" ya da \"Kısmen Yanıtlandı\" cevabı verilen sorular Açık Konular'a aktarılır.",
            error.Message);
    }

    [Fact]
    public async Task Transfer_UnansweredQuestion_Throws400()
    {
        var projectId = await TestProjects.CreateAsync(database, "6");
        var question = (await FormAsync(projectId)).Questions[0].Question;

        await Assert.ThrowsAsync<BusinessRuleException>(() => TransferAsync(projectId, question.Id));
    }

    [Fact]
    public async Task Transfer_QuestionNotOnTheForm_Throws404()
    {
        var projectId = await TestProjects.CreateAsync(database, "6");

        var error = await Assert.ThrowsAsync<NotFoundException>(() => TransferAsync(projectId, int.MaxValue));

        Assert.Equal("Soru bu formda yer almıyor.", error.Message);
    }

    [Fact]
    public async Task Transfer_ProjectNotActive_Throws409()
    {
        var projectId = await TestProjects.CreateAsync(database, null, ProjectStatus.Rejected);

        await Assert.ThrowsAsync<ConflictException>(() => TransferAsync(projectId, 1));
    }

    // ---- Header: Görüşme Tarihi & Yöntemi ----

    [Fact]
    public async Task SaveHeader_Method_IsKeptTrimmed()
    {
        var projectId = await TestProjects.CreateAsync(database, "6");

        var form = await AsAsync((q, _) => q.SaveAsync(projectId, Set, new SaveQuestionnaireDto
        {
            Subcategory = RequestSource.Internal,
            Participants = "Elif Yılmaz, İK Müdürü",
            AssessedOn = new DateOnly(2026, 11, 13),
            Method = "  Yüz yüze toplantı  ",
        }, default));

        Assert.Equal("Yüz yüze toplantı", form.Method);
        Assert.Equal(new DateOnly(2026, 11, 13), form.AssessedOn);
    }
}
