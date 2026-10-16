using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;

/// <summary>Finish times and positions: captured by Organisers, viewed by everyone / the Participant.</summary>
[Route("api")]
[SessionAuthorize]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
public class ResultsController : ApiControllerBase
{
    private const string TimeFormat = @"hh\:mm\:ss";
    private readonly AppDbContext _db;

    public ResultsController(AppDbContext db) => _db = db;

    /// <summary>Capture a Participant's finish time and position after an event.</summary>
    /// <remarks>Organiser only, and only for their own events. The event must already have taken place.
    /// Each enrolment can have one result and each finishing position can be used once per event.</remarks>
    /// <response code="201">Result recorded.</response>
    /// <response code="400">Validation failed, invalid time, or the event has not taken place yet.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or the event belongs to another Organiser.</response>
    /// <response code="404">Event or enrolment (for this event) does not exist.</response>
    /// <response code="409">The enrolment already has a result, or the position is already taken.</response>
    [HttpPost("events/{eventId:int}/results")]
    [SessionAuthorize(RoleNames.Organiser)]
    [ProducesResponseType(typeof(ResultResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ResultResponse>> Capture(int eventId, [FromBody] ResultRequest request)
    {
        var ev = await _db.Events.AsNoTracking().SingleOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null) return Fail(404, "Event not found");
        if (ev.OrganiserId != CurrentUserId)
            return Fail(403, "Access denied", "You can only capture results for events that you created.");
        if (ev.EventDate > DateTime.UtcNow)
            return Fail(400, "Event not finished", "Results can only be captured after the event has taken place.");

        if (!TimeSpan.TryParseExact(request.FinishTime, new[] { TimeFormat, @"h\:mm\:ss" },
                CultureInfo.InvariantCulture, out var finishTime) || finishTime <= TimeSpan.Zero)
            return Fail(400, "Invalid finish time", "Use the format hh:mm:ss with a time greater than zero.");

        var enrolment = await _db.Enrolments
            .SingleOrDefaultAsync(e => e.EnrolmentId == request.EnrolmentId && e.EventId == eventId);
        if (enrolment is null) return Fail(404, "Enrolment not found", "No such enrolment exists for this event.");

        if (await _db.Results.AnyAsync(r => r.EnrolmentId == enrolment.EnrolmentId))
            return Fail(409, "Result already captured", "This enrolment already has a result.");
        if (await _db.Results.AnyAsync(r => r.Enrolment.EventId == eventId && r.FinishPosition == request.FinishPosition))
            return Fail(409, "Position already taken", "Another participant already has that finishing position.");

        var result = new RaceResult
        {
            EnrolmentId = enrolment.EnrolmentId,
            FinishTime = finishTime,
            FinishPosition = request.FinishPosition,
            RecordedAt = DateTime.UtcNow
        };
        _db.Results.Add(result);
        await _db.SaveChangesAsync();

        var dto = (await ToResponses(_db.Results.AsNoTracking().Where(r => r.ResultId == result.ResultId))).Single();
        return Created($"/api/events/{eventId}/results", dto);
    }

    /// <summary>List the published results of an event, ordered by finishing position.</summary>
    /// <remarks>Any logged-in user.</remarks>
    /// <response code="200">Results for the event (may be empty).</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="404">Event does not exist.</response>
    [HttpGet("events/{eventId:int}/results")]
    [ProducesResponseType(typeof(List<ResultResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<ResultResponse>>> GetForEvent(int eventId)
    {
        if (!await _db.Events.AnyAsync(e => e.EventId == eventId)) return Fail(404, "Event not found");

        return await ToResponses(_db.Results.AsNoTracking()
            .Where(r => r.Enrolment.EventId == eventId)
            .OrderBy(r => r.FinishPosition));
    }

    /// <summary>List the logged-in Participant's own results across all completed events.</summary>
    /// <remarks>Participant only. Includes finish time, position and the number of finishers (e.g. 47th of 312).</remarks>
    /// <response code="200">The Participant's results, most recent event first.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in as an Organiser.</response>
    [HttpGet("results/mine")]
    [SessionAuthorize(RoleNames.Participant)]
    [ProducesResponseType(typeof(List<ResultResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<ResultResponse>>> GetMine()
    {
        var uid = CurrentUserId;
        return await ToResponses(_db.Results.AsNoTracking()
            .Where(r => r.Enrolment.UserId == uid)
            .OrderByDescending(r => r.Enrolment.Event.EventDate));
    }

    /// <summary>Turns a (pre-filtered, pre-ordered) results query into DTOs including the total finishers per event.</summary>
    private async Task<List<ResultResponse>> ToResponses(IQueryable<RaceResult> query)
    {
        var rows = await query.Select(r => new
        {
            r.ResultId,
            r.EnrolmentId,
            EventId = r.Enrolment.EventId,
            EventName = r.Enrolment.Event.Name,
            EventDate = r.Enrolment.Event.EventDate,
            Participant = r.Enrolment.User.FirstName + " " + r.Enrolment.User.LastName,
            Category = r.Enrolment.Category.Name,
            r.FinishTime,
            r.FinishPosition
        }).ToListAsync();

        var eventIds = rows.Select(r => r.EventId).Distinct().ToList();
        var totals = await _db.Results
            .Where(r => eventIds.Contains(r.Enrolment.EventId))
            .GroupBy(r => r.Enrolment.EventId)
            .Select(g => new { EventId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.EventId, x => x.Count);

        return rows.Select(r => new ResultResponse(
            r.ResultId, r.EnrolmentId, r.EventId, r.EventName, r.EventDate, r.Participant, r.Category,
            r.FinishTime.ToString(TimeFormat), r.FinishPosition,
            totals.TryGetValue(r.EventId, out var total) ? total : 0)).ToList();
    }
}
