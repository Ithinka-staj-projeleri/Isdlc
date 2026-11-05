using Isdlc.Application.Common.Models;
using Isdlc.Application.Meetings.Dtos;

namespace Isdlc.Application.Meetings;

/// <summary>
/// The project's meeting minutes. Contract: docs/open-issues-meetings-api.md
/// Common errors: 404 project or meeting missing, 409 project not active (writes only).
/// </summary>
public interface IMeetingService
{
    /// <summary>Newest meeting first.</summary>
    Task<PagedResult<MeetingListItemDto>> ListAsync(
        int projectId, PageRequest pageRequest, CancellationToken cancellationToken);

    Task<MeetingDto> GetAsync(int projectId, int meetingId, CancellationToken cancellationToken);

    /// <summary>
    /// Adds the minutes with the next code (TT-001...) and turns its decisions and actions into open issues,
    /// all in one save. 409 if a code was taken by another record at the same moment.
    /// </summary>
    Task<MeetingDto> CreateAsync(int projectId, CreateMeetingDto request, CancellationToken cancellationToken);

    Task<MeetingDto> UpdateAsync(
        int projectId, int meetingId, UpdateMeetingDto request, CancellationToken cancellationToken);
}
