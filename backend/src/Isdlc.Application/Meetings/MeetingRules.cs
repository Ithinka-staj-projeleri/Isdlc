using Isdlc.Application.Common.Exceptions;
using Isdlc.Application.Meetings.Dtos;
using Isdlc.Application.OpenIssues;
using Isdlc.Domain.Meetings;

namespace Isdlc.Application.Meetings;

/// <summary>
/// The rules of the minutes' fields that attributes cannot express (docs/open-issues-meetings-api.md → Faz 4 · E4).
/// Pure: no database, every case unit tested.
/// </summary>
public static class MeetingRules
{
    public const string VisitFieldsOnlyOnVisit = "Dönüş tarihi ve aktiviteler yalnızca müşteri ziyaretinde girilir.";
    public const string ReturnDateRequired = "Ziyarette dönüş tarihi zorunludur.";
    public const string ReturnBeforeDeparture = "Dönüş tarihi gidiş tarihinden önce olamaz.";

    public static bool IsVisit(MeetingType type) => type == MeetingType.CustomerVisit;

    /// <summary>
    /// A visit needs its return date, on or after the departure (Date). Other minutes take no visit fields:
    /// they are cleared when the type is changed from a visit, so a value sent with another type is a mistake (400).
    /// </summary>
    public static void CheckVisitFields(
        MeetingType type, DateOnly date, DateOnly? returnDate, string? plannedActivities, string? completedActivities)
    {
        if (!IsVisit(type))
        {
            if (returnDate is not null
                || OpenIssueRules.Clean(plannedActivities) is not null
                || OpenIssueRules.Clean(completedActivities) is not null)
            {
                throw new BusinessRuleException(VisitFieldsOnlyOnVisit);
            }

            return;
        }

        if (returnDate is not { } back)
        {
            throw new BusinessRuleException(ReturnDateRequired);
        }

        if (back < date)
        {
            throw new BusinessRuleException(ReturnBeforeDeparture);
        }
    }

    /// <summary>Trimmed, in the order entered; an empty role is no role. Names are required by the DTO.</summary>
    public static List<MeetingParticipant> ToParticipants(IEnumerable<MeetingParticipantDto> participants) =>
        participants
            .Select(p => new MeetingParticipant(p.Name!.Trim(), OpenIssueRules.Clean(p.Role)))
            .ToList();

    /// <summary>"Elif Yılmaz, Can Öztürk" for the list.</summary>
    public static string NamesOf(IEnumerable<MeetingParticipant> participants) =>
        string.Join(", ", participants.Select(p => p.Name));
}
