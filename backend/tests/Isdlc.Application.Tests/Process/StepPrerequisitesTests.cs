using Isdlc.Application.Process;
using Isdlc.Domain.Process;

namespace Isdlc.Application.Tests.Process;

/// <summary>
/// Whether a step's prerequisites hold, the message and where it leads (Faz 7 · A4). No database.
/// Rules: docs/configurability.md → Adım ön koşulları.
/// </summary>
public class StepPrerequisitesTests
{
    private const int ProjectId = 11;

    private static PrerequisiteFacts Facts(
        bool requestForm = false,
        string[]? completedForms = null,
        bool level = false,
        int meetings = 0,
        int risks = 0,
        int requirements = 0,
        int unapprovedRequirements = 0,
        string[]? approvedDocuments = null) =>
        new(requestForm, (completedForms ?? []).ToHashSet(), level, meetings, risks, requirements, unapprovedRequirements,
            (approvedDocuments ?? []).ToHashSet(),
            new Dictionary<string, string> { ["customer-fit"] = "Müşteri Uygunluk Değerlendirme Anket Formu" },
            new Dictionary<string, string> { ["PKD"] = "Proje Kapsam Dokümanı" });

    private static StepPrerequisite Of(PrerequisiteKind kind, string? value = null) => new(kind, value);

    [Fact]
    public void Evaluate_NoPrerequisites_NothingIsMissing()
    {
        Assert.Empty(StepPrerequisites.Evaluate([], Facts(), ProjectId));
    }

    public static TheoryData<StepPrerequisite, PrerequisiteFacts, PrerequisiteFacts> HoldingAndMissing => new()
    {
        { Of(PrerequisiteKind.RequestFormComplete), Facts(requestForm: true), Facts() },
        { Of(PrerequisiteKind.QuestionnaireComplete, "customer-fit"), Facts(completedForms: ["customer-fit"]), Facts(completedForms: ["level"]) },
        { Of(PrerequisiteKind.LevelApplied), Facts(level: true), Facts() },
        { Of(PrerequisiteKind.MeetingExists), Facts(meetings: 1), Facts() },
        { Of(PrerequisiteKind.RiskExists), Facts(risks: 2), Facts() },
        { Of(PrerequisiteKind.RequirementsApproved), Facts(requirements: 3), Facts(requirements: 3, unapprovedRequirements: 1) },
        { Of(PrerequisiteKind.DocumentApproved, "PKD"), Facts(approvedDocuments: ["PKD"]), Facts(approvedDocuments: ["BRD"]) },
    };

    [Theory]
    [MemberData(nameof(HoldingAndMissing))]
    public void Holds_EachKind_FollowsTheProject(StepPrerequisite prerequisite, PrerequisiteFacts holding, PrerequisiteFacts missing)
    {
        Assert.True(StepPrerequisites.Holds(prerequisite, holding));
        Assert.False(StepPrerequisites.Holds(prerequisite, missing));
    }

    [Fact]
    public void Holds_RequirementsApproved_NeedsAtLeastOneRequirement()
    {
        // No requirements at all is not "approved": the step asks for the work to be done.
        Assert.False(StepPrerequisites.Holds(Of(PrerequisiteKind.RequirementsApproved), Facts()));
    }

    [Theory]
    [InlineData(PrerequisiteKind.QuestionnaireComplete)]
    [InlineData(PrerequisiteKind.DocumentApproved)]
    public void Holds_KindThatNeedsAValue_WithoutOne_NeverHolds(PrerequisiteKind kind)
    {
        Assert.False(StepPrerequisites.Holds(Of(kind), Facts(completedForms: ["customer-fit"], approvedDocuments: ["PKD"])));
    }

    [Fact]
    public void Evaluate_ListsOnlyTheMissingOnes_InTheStepsOrder()
    {
        var unmet = StepPrerequisites.Evaluate(
            [Of(PrerequisiteKind.MeetingExists), Of(PrerequisiteKind.LevelApplied), Of(PrerequisiteKind.RiskExists)],
            Facts(level: true),
            ProjectId);

        Assert.Equal([PrerequisiteKind.MeetingExists, PrerequisiteKind.RiskExists], unmet.Select(u => u.Kind));
    }

    public static TheoryData<StepPrerequisite, string, string, string> Messages => new()
    {
        { Of(PrerequisiteKind.RequestFormComplete), "Talep Kayıt Formu'nun zorunlu alanları doldurulmalı.", "Talep formuna git", "/projects/11/edit" },
        { Of(PrerequisiteKind.QuestionnaireComplete, "customer-fit"), "Müşteri Uygunluk Değerlendirme Anket Formu tamamlanmalı.", "Seviye & Uygunluk'a git", "/projects/11?tab=assessment" },
        { Of(PrerequisiteKind.QuestionnaireComplete, "request-analysis"), "request-analysis tamamlanmalı.", "Talep Analizi'ne git", "/projects/11?tab=request-analysis" },
        { Of(PrerequisiteKind.LevelApplied), "Seviye projeye uygulanmalı.", "Seviye & Uygunluk'a git", "/projects/11?tab=assessment" },
        { Of(PrerequisiteKind.MeetingExists), "En az bir toplantı tutanağı girilmeli.", "Tutanaklara git", "/projects/11?tab=meetings" },
        { Of(PrerequisiteKind.RiskExists), "En az bir risk girilmeli.", "Risklere git", "/projects/11?tab=risks" },
        { Of(PrerequisiteKind.RequirementsApproved), "Gereksinimler onaylanmalı; taslak ya da incelemede gereksinim kalmamalı.", "Gereksinimlere git", "/projects/11?tab=requirements" },
        { Of(PrerequisiteKind.DocumentApproved, "PKD"), "«Proje Kapsam Dokümanı» onaylanmalı.", "Dokümanlara git", "/projects/11?tab=documents" },
    };

    [Theory]
    [MemberData(nameof(Messages))]
    public void Describe_EachKind_SaysWhatIsMissingAndWhereToDoIt(
        StepPrerequisite prerequisite, string message, string linkText, string link)
    {
        var unmet = StepPrerequisites.Describe(prerequisite, Facts(), ProjectId);

        Assert.Equal(message, unmet.Message);
        Assert.Equal(linkText, unmet.LinkText);
        Assert.Equal(link, unmet.Link);
    }

    [Fact]
    public void BlockedMessage_NamesTheFirstMissingOne()
    {
        var unmet = StepPrerequisites.Evaluate(
            [Of(PrerequisiteKind.LevelApplied), Of(PrerequisiteKind.MeetingExists)], Facts(), ProjectId);

        Assert.Equal("Bu adım tamamlanamaz: Seviye projeye uygulanmalı.", StepPrerequisites.BlockedMessage(unmet));
    }
}
