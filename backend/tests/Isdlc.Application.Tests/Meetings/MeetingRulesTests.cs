using System.ComponentModel.DataAnnotations;
using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Meetings;
using Isdlc.Application.Meetings.Dtos;
using Isdlc.Domain.Meetings;

namespace Isdlc.Application.Tests.Meetings;

/// <summary>
/// The minutes' field rules without a database: visit fields, participants and the request attributes.
/// Contract: docs/open-issues-meetings-api.md → Faz 4 · E4.
/// </summary>
public class MeetingRulesTests
{
    private static readonly DateOnly Departure = new(2026, 11, 18);

    private static void Check(MeetingType type, DateOnly? returnDate = null, string? planned = null, string? done = null) =>
        MeetingRules.CheckVisitFields(type, Departure, returnDate, planned, done);

    // ---- Visit fields ----

    [Theory]
    [InlineData(0)]  // same day
    [InlineData(1)]
    [InlineData(10)]
    public void CheckVisitFields_VisitReturningOnOrAfterDeparture_IsValid(int days)
    {
        Check(MeetingType.CustomerVisit, Departure.AddDays(days), "Sayımı izleme", "Sayım izlendi");
    }

    [Fact]
    public void CheckVisitFields_VisitWithoutActivities_IsValid()
    {
        Check(MeetingType.CustomerVisit, Departure);
    }

    [Fact]
    public void CheckVisitFields_VisitWithoutReturnDate_Throws()
    {
        var ex = Assert.Throws<BusinessRuleException>(() => Check(MeetingType.CustomerVisit));

        Assert.Equal(MeetingRules.ReturnDateRequired, ex.Message);
    }

    [Fact]
    public void CheckVisitFields_ReturnBeforeDeparture_Throws()
    {
        var ex = Assert.Throws<BusinessRuleException>(() => Check(MeetingType.CustomerVisit, Departure.AddDays(-1)));

        Assert.Equal(MeetingRules.ReturnBeforeDeparture, ex.Message);
    }

    public static TheoryData<MeetingType> OtherTypes() =>
        new(Enum.GetValues<MeetingType>().Where(t => t != MeetingType.CustomerVisit));

    [Theory]
    [MemberData(nameof(OtherTypes))]
    public void CheckVisitFields_OtherTypeWithoutVisitFields_IsValid(MeetingType type)
    {
        Check(type, planned: "   ", done: "");  // blank text is no value
    }

    [Theory]
    [MemberData(nameof(OtherTypes))]
    public void CheckVisitFields_OtherTypeWithAnyVisitField_Throws(MeetingType type)
    {
        Assert.Equal(MeetingRules.VisitFieldsOnlyOnVisit,
            Assert.Throws<BusinessRuleException>(() => Check(type, returnDate: Departure)).Message);
        Assert.Equal(MeetingRules.VisitFieldsOnlyOnVisit,
            Assert.Throws<BusinessRuleException>(() => Check(type, planned: "Depo turu")).Message);
        Assert.Equal(MeetingRules.VisitFieldsOnlyOnVisit,
            Assert.Throws<BusinessRuleException>(() => Check(type, done: "Depo turu yapıldı")).Message);
    }

    // ---- Participants ----

    [Fact]
    public void ToParticipants_TrimsKeepsOrder_AndBlankRoleIsNoRole()
    {
        var participants = MeetingRules.ToParticipants(
        [
            new MeetingParticipantDto { Name = "  Hakan Yıldız ", Role = " Depo Müdürü (Demo Lojistik) " },
            new MeetingParticipantDto { Name = "Elif Yılmaz", Role = "  " },
            new MeetingParticipantDto { Name = "Can Öztürk" },
        ]);

        Assert.Equal(
            [
                new MeetingParticipant("Hakan Yıldız", "Depo Müdürü (Demo Lojistik)"),
                new MeetingParticipant("Elif Yılmaz", null),
                new MeetingParticipant("Can Öztürk", null),
            ],
            participants);
    }

    [Fact]
    public void NamesOf_JoinsNamesWithoutRoles()
    {
        Assert.Equal("Elif Yılmaz, Hakan Yıldız",
            MeetingRules.NamesOf([new MeetingParticipant("Elif Yılmaz", "İş Analisti"), new MeetingParticipant("Hakan Yıldız", null)]));
        Assert.Equal("", MeetingRules.NamesOf([]));
    }

    // ---- Request attributes (the API turns these into errors["participants"], errors["participants[0].name"]) ----

    private static List<ValidationResult> Validate(object dto)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);
        return results;
    }

    private static UpdateMeetingDto ValidUpdate() => new()
    {
        Type = MeetingType.AnalysisSession,
        Date = Departure,
        RecordedByUserId = 1,
        Participants = [new MeetingParticipantDto { Name = "Elif Yılmaz" }],
        Agenda = "Kapsam",
    };

    [Fact]
    public void UpdateDto_RequiredFieldsOnly_IsValid()
    {
        Assert.Empty(Validate(ValidUpdate()));
    }

    [Fact]
    public void UpdateDto_NoRecorder_HasTurkishMessage()
    {
        var error = Assert.Single(Validate(ValidUpdate() with { RecordedByUserId = null }));

        Assert.Equal("Tutanağı yazan kişi seçilmelidir.", error.ErrorMessage);
    }

    [Fact]
    public void UpdateDto_NoParticipants_HasTurkishMessage()
    {
        Assert.Equal("En az bir katılımcı girilmelidir.", Assert.Single(Validate(ValidUpdate() with { Participants = [] })).ErrorMessage);
        Assert.Equal("En az bir katılımcı girilmelidir.", Assert.Single(Validate(ValidUpdate() with { Participants = null })).ErrorMessage);
    }

    [Fact]
    public void UpdateDto_ThirtyParticipants_IsValid_ThirtyOne_HasTurkishMessage()
    {
        MeetingParticipantDto[] Many(int count) =>
            Enumerable.Range(1, count).Select(i => new MeetingParticipantDto { Name = $"Kişi {i}" }).ToArray();

        Assert.Empty(Validate(ValidUpdate() with { Participants = Many(30) }));
        Assert.Equal("En fazla 30 katılımcı girilebilir.",
            Assert.Single(Validate(ValidUpdate() with { Participants = Many(31) })).ErrorMessage);
    }

    [Theory]
    [InlineData(nameof(UpdateMeetingDto.Summary), 4000, "Toplantı özeti en fazla 4000 karakter olabilir.")]
    [InlineData(nameof(UpdateMeetingDto.CompletedStages), 2000, "Tamamlanan aşamalar en fazla 2000 karakter olabilir.")]
    [InlineData(nameof(UpdateMeetingDto.OngoingStages), 2000, "Devam eden aşamalar en fazla 2000 karakter olabilir.")]
    [InlineData(nameof(UpdateMeetingDto.NextSteps), 2000, "Sonraki adımlar en fazla 2000 karakter olabilir.")]
    [InlineData(nameof(UpdateMeetingDto.PlannedActivities), 4000, "Planlanan aktiviteler en fazla 4000 karakter olabilir.")]
    [InlineData(nameof(UpdateMeetingDto.CompletedActivities), 4000, "Yapılan aktiviteler en fazla 4000 karakter olabilir.")]
    public void UpdateDto_TextAtItsLimit_IsValid_OneMore_HasTurkishMessage(string field, int limit, string message)
    {
        UpdateMeetingDto With(int length)
        {
            var dto = ValidUpdate();
            typeof(UpdateMeetingDto).GetProperty(field)!.SetValue(dto, new string('a', length));
            return dto;
        }

        Assert.Empty(Validate(With(limit)));
        var error = Assert.Single(Validate(With(limit + 1)));
        Assert.Equal([field], error.MemberNames);
        Assert.Equal(message, error.ErrorMessage);
    }

    [Fact]
    public void ParticipantDto_BlankName_AndLongTexts_HaveTurkishMessages()
    {
        Assert.Equal("Katılımcının adı zorunludur.",
            Assert.Single(Validate(new MeetingParticipantDto { Name = "  " })).ErrorMessage);
        Assert.Equal("Katılımcının adı en fazla 100 karakter olabilir.",
            Assert.Single(Validate(new MeetingParticipantDto { Name = new string('a', 101) })).ErrorMessage);
        Assert.Equal("Rol en fazla 100 karakter olabilir.",
            Assert.Single(Validate(new MeetingParticipantDto { Name = "Elif", Role = new string('a', 101) })).ErrorMessage);
        Assert.Empty(Validate(new MeetingParticipantDto { Name = new string('a', 100), Role = new string('a', 100) }));
    }
}
