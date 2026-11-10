using Isdlc.Application.Audit;

namespace Isdlc.Application.Tests.Audit;

/// <summary>
/// The pure reader of AuditLog.Changes (no database). Expectations: docs/audit-api.md → DTO.
/// Cross test (PLANNING.md): Dide → Erva's modules.
/// </summary>
public class AuditChangeReaderTests
{
    [Fact]
    public void Read_KeepsTheStoredOrder_AndLeavesOutProjectId()
    {
        var fields = AuditChangeReader.Read("""
            {"ProjectId":{"old":null,"new":7},"Title":{"old":null,"new":"Başlık"},"Status":{"old":null,"new":"Open"}}
            """);

        Assert.Equal(["Title", "Status"], fields.Select(f => f.Name));
    }

    [Fact]
    public void Read_TurnsEveryValueKindIntoText()
    {
        var fields = AuditChangeReader.Read("""
            {
              "Status": {"old": "Open", "new": "Closed"},
              "Probability": {"old": 3, "new": 4},
              "Score": {"old": 12.5, "new": 3.85},
              "RequiresNda": {"old": false, "new": true},
              "DueDate": {"old": null, "new": "2026-11-12"}
            }
            """);

        Assert.Equal(
        [
            new AuditChangeReader.Field("Status", "Open", "Closed"),
            new AuditChangeReader.Field("Probability", "3", "4"),
            new AuditChangeReader.Field("Score", "12.5", "3.85"),
            new AuditChangeReader.Field("RequiresNda", "false", "true"),
            new AuditChangeReader.Field("DueDate", null, "2026-11-12"),
        ], fields);
    }

    [Fact]
    public void Read_MissingOldOrNew_IsNull()
    {
        var field = Assert.Single(AuditChangeReader.Read("""{"Title":{"new":"Başlık"}}"""));

        Assert.Equal(new AuditChangeReader.Field("Title", null, "Başlık"), field);
    }

    [Fact]
    public void Read_FieldWhoseChangeIsNotAnObject_HasNoValues()
    {
        var field = Assert.Single(AuditChangeReader.Read("""{"Title":"Başlık"}"""));

        Assert.Equal(new AuditChangeReader.Field("Title", null, null), field);
    }

    // The log is history: a broken row must not stop the whole history from opening.
    [Theory]
    [InlineData("")]
    [InlineData("bozuk")]
    [InlineData("{\"Title\":")]
    [InlineData("[]")]
    [InlineData("\"metin\"")]
    [InlineData("{}")]
    public void Read_BrokenOrEmptyJson_ReturnsNoFields(string json)
    {
        Assert.Empty(AuditChangeReader.Read(json));
    }

    [Theory]
    [InlineData("12", 12)]
    [InlineData("-5", -5)]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("Ayşe Demir", null)]
    [InlineData("3.5", null)]
    public void IdOf_ReadsOnlyWholeNumbers(string? value, int? expected)
    {
        Assert.Equal(expected, AuditChangeReader.IdOf(value));
    }

    [Fact]
    public void ReferenceFields_AreTheOnesTheContractLists()
    {
        Assert.Equal(["BusinessAnalystId", "OwnerUserId", "ProjectManagerId"], AuditChangeReader.UserFields.Order());
        Assert.Equal("CurrentStepId", AuditChangeReader.StepField);
        Assert.Equal("SourceMeetingId", AuditChangeReader.MeetingField);
        Assert.Equal("DocumentTypeId", AuditChangeReader.DocumentTypeField);
    }
}
