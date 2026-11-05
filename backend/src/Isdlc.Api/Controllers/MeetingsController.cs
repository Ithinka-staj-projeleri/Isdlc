using Isdlc.Api.Common;
using Isdlc.Application.Common.Models;
using Isdlc.Application.Meetings;
using Isdlc.Application.Meetings.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Isdlc.Api.Controllers;

/// <summary>
/// The project's meeting minutes: /api/v1/projects/{projectId}/meetings.
/// It is a sub-resource of projects, so the routes are written in full ("/" ignores the base route).
/// </summary>
public class MeetingsController(IMeetingService meetingService) : ApiControllerBase
{
    private const string Route = "/api/v1/projects/{projectId:int}/meetings";

    /// <summary>Newest meeting first. Paging: ?page=1&amp;pageSize=20</summary>
    [HttpGet(Route)]
    [ProducesResponseType<PagedResult<MeetingListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<PagedResult<MeetingListItemDto>> List(
        int projectId, [FromQuery] PageRequest pageRequest, CancellationToken cancellationToken)
    {
        return await meetingService.ListAsync(projectId, pageRequest, cancellationToken);
    }

    [HttpGet(Route + "/{meetingId:int}")]
    [ProducesResponseType<MeetingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<MeetingDto> Get(int projectId, int meetingId, CancellationToken cancellationToken)
    {
        return await meetingService.GetAsync(projectId, meetingId, cancellationToken);
    }

    /// <summary>Adds the minutes; its decisions and actions become open issues of the project.</summary>
    [HttpPost(Route)]
    [ProducesResponseType<MeetingDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MeetingDto>> Create(
        int projectId, CreateMeetingDto request, CancellationToken cancellationToken)
    {
        var created = await meetingService.CreateAsync(projectId, request, cancellationToken);
        return Created($"/api/v1/projects/{projectId}/meetings/{created.Id}", created);
    }

    /// <summary>Updates type, date, participants and agenda. Decisions and actions are managed as open issues.</summary>
    [HttpPut(Route + "/{meetingId:int}")]
    [ProducesResponseType<MeetingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<MeetingDto> Update(
        int projectId, int meetingId, UpdateMeetingDto request, CancellationToken cancellationToken)
    {
        return await meetingService.UpdateAsync(projectId, meetingId, request, cancellationToken);
    }
}
