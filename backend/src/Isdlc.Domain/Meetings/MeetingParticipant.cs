namespace Isdlc.Domain.Meetings;

/// <summary>
/// Someone at the meeting: a name and, if given, a role / unit / company ("Depo Müdürü (Demo Lojistik)").
/// Not a system user, so the customer's side can be written too. Kept as a JSON list on the minutes
/// (Meeting.Participants), so changing the list never deletes rows.
/// </summary>
public record MeetingParticipant(string Name, string? Role)
{
    public const int MaxCount = 30;
    public const int MaxNameLength = 100;
    public const int MaxRoleLength = 100;
}
