using System.Linq.Expressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;

/// <summary>Participants enrol in events; Organisers view the enrolments for their events.</summary>
[Route("api")]
[SessionAuthorize]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
public class EnrolmentsController : ApiControllerBase
{
    private readonly AppDbContext _db;

    public EnrolmentsController(AppDbContext db) => _db = db;

    private static readonly Expression<Func<Enrolment, EnrolmentResponse>> ToResponse = e => new EnrolmentResponse(
        e.EnrolmentId, e.UserId, e.User.FirstName + " " + e.User.LastName, e.EventId, e.Event.Name,
        e.Event.EventDate, e.CategoryId, e.Category.Name, e.EnrolledAt, e.Status);

    /// <summary>Enrol the logged-in Participant in an event by choosing a category.</summary>
    /// <remarks>Participant only. The enrolment is recorded immediately with status Confirmed.
    /// The category must belong to the event, the event must be in the future, and a Participant can enrol once per event.</remarks>
    /// <response code="201">Enrolment recorded.</response>
    /// <response code="400">Category does not belong to this event, or the event has already taken place.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in as an Organiser.</response>
    /// <response code="404">Event does not exist.</response>
    /// <response code="409">You are already enrolled in this event.</response>
    [HttpPost("events/{eventId:int}/enrolments")]
    [SessionAuthorize(RoleNames.Participant)]
    [ProducesResponseType(typeof(EnrolmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EnrolmentResponse>> Enrol(int eventId, [FromBody] EnrolRequest request)
    {
        var ev = await _db.Events.AsNoTracking().SingleOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null) return Fail(404, "Event not found");
        if (ev.EventDate <= DateTime.UtcNow)
            return Fail(400, "Event closed", "You cannot enrol in an event that has already taken place.");

        var category = await _db.Categories.AsNoTracking()
            .SingleOrDefaultAsync(c => c.CategoryId == request.CategoryId);
        if (category is null || category.EventId != eventId)
            return Fail(400, "Invalid category", "The selected category does not belong to this event.");

        var uid = CurrentUserId;
        if (await _db.Enrolments.AnyAsync(e => e.UserId == uid && e.EventId == eventId))
            return Fail(409, "Already enrolled", "You are already enrolled in this event.");

        var enrolment = new Enrolment
        {
            UserId = uid,
            EventId = eventId,
            CategoryId = category.CategoryId,
            EnrolledAt = DateTime.UtcNow,
            Status = EnrolmentStatus.Confirmed
        };
        _db.Enrolments.Add(enrolment);
        await _db.SaveChangesAsync();

        var dto = await _db.Enrolments.AsNoTracking()
            .Where(e => e.EnrolmentId == enrolment.EnrolmentId).Select(ToResponse).SingleAsync();
        return Created("/api/enrolments/mine", dto);
    }

    /// <summary>List every event the logged-in Participant has enrolled in, with status.</summary>
    /// <remarks>Participant only.</remarks>
    /// <response code="200">The Participant's enrolments, soonest event first.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in as an Organiser.</response>
    [HttpGet("enrolments/mine")]
    [SessionAuthorize(RoleNames.Participant)]
    [ProducesResponseType(typeof(List<EnrolmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<EnrolmentResponse>>> GetMine()
    {
        var uid = CurrentUserId;
        return await _db.Enrolments.AsNoTracking()
            .Where(e => e.UserId == uid)
            .OrderBy(e => e.Event.EventDate)
            .Select(ToResponse)
            .ToListAsync();
    }

    /// <summary>List all enrolments for an event you own.</summary>
    /// <remarks>Organiser only, and only for their own events.</remarks>
    /// <response code="200">Enrolments for the event.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or the event belongs to another Organiser.</response>
    /// <response code="404">Event does not exist.</response>
    [HttpGet("events/{eventId:int}/enrolments")]
    [SessionAuthorize(RoleNames.Organiser)]
    [ProducesResponseType(typeof(List<EnrolmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<EnrolmentResponse>>> GetForEvent(int eventId)
    {
        var ev = await _db.Events.AsNoTracking().SingleOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null) return Fail(404, "Event not found");
        if (ev.OrganiserId != CurrentUserId)
            return Fail(403, "Access denied", "You can only view enrolments for events that you created.");

        return await _db.Enrolments.AsNoTracking()
            .Where(e => e.EventId == eventId)
            .OrderBy(e => e.EnrolledAt)
            .Select(ToResponse)
            .ToListAsync();
    }

    /// <summary>Change the status (Confirmed / Pending) of an enrolment for an event you own.</summary>
    /// <remarks>Organiser only, and only for their own events.</remarks>
    /// <response code="200">Status updated.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or the event belongs to another Organiser.</response>
    /// <response code="404">Enrolment does not exist.</response>
    [HttpPut("enrolments/{id:int}/status")]
    [SessionAuthorize(RoleNames.Organiser)]
    [ProducesResponseType(typeof(EnrolmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EnrolmentResponse>> UpdateStatus(int id, [FromBody] UpdateEnrolmentStatusRequest request)
    {
        var enrolment = await _db.Enrolments.Include(e => e.Event).SingleOrDefaultAsync(e => e.EnrolmentId == id);
        if (enrolment is null) return Fail(404, "Enrolment not found");
        if (enrolment.Event.OrganiserId != CurrentUserId)
            return Fail(403, "Access denied", "You can only change enrolments for events that you created.");

        enrolment.Status = request.Status!.Value;
        await _db.SaveChangesAsync();

        return await _db.Enrolments.AsNoTracking().Where(e => e.EnrolmentId == id).Select(ToResponse).SingleAsync();
    }
}
