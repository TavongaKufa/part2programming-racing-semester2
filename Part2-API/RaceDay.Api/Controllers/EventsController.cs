using System.Linq.Expressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;

/// <summary>Create, view, update and delete race events.</summary>
[Route("api/events")]
[SessionAuthorize]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
public class EventsController : ApiControllerBase
{
    private readonly AppDbContext _db;

    public EventsController(AppDbContext db) => _db = db;

    private static readonly Expression<Func<Event, EventResponse>> ToResponse = e => new EventResponse(
        e.EventId, e.Name, e.Description, e.EventDate, e.Location, e.DistanceKm, e.EventType,
        e.OrganiserId, e.Organiser.FirstName + " " + e.Organiser.LastName, e.Enrolments.Count());

    /// <summary>List all events, soonest first, with optional filters.</summary>
    /// <remarks>Any logged-in user. Filters: type (Run/Walk/Cycle), upcomingOnly, search (name or location).</remarks>
    /// <response code="200">List of events (may be empty).</response>
    /// <response code="401">Not logged in.</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<EventResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<EventResponse>>> GetAll(
        [FromQuery] EventType? type, [FromQuery] bool upcomingOnly = false, [FromQuery] string? search = null)
    {
        var query = _db.Events.AsNoTracking().AsQueryable();
        if (type.HasValue) query = query.Where(e => e.EventType == type.Value);
        if (upcomingOnly) query = query.Where(e => e.EventDate >= DateTime.UtcNow);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(e => e.Name.Contains(term) || e.Location.Contains(term));
        }
        return await query.OrderBy(e => e.EventDate).Select(ToResponse).ToListAsync();
    }

    /// <summary>List the events created by the logged-in Organiser (dashboard).</summary>
    /// <remarks>Organiser only. Includes the enrolment count for each event.</remarks>
    /// <response code="200">The Organiser's events.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in as a Participant.</response>
    [HttpGet("mine")]
    [SessionAuthorize(RoleNames.Organiser)]
    [ProducesResponseType(typeof(List<EventResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<EventResponse>>> GetMine()
    {
        var uid = CurrentUserId;
        return await _db.Events.AsNoTracking()
            .Where(e => e.OrganiserId == uid)
            .OrderBy(e => e.EventDate)
            .Select(ToResponse)
            .ToListAsync();
    }

    /// <summary>Get a single event by id.</summary>
    /// <remarks>Any logged-in user.</remarks>
    /// <response code="200">The event.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="404">Event does not exist.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventResponse>> GetById(int id)
    {
        var ev = await _db.Events.AsNoTracking().Where(e => e.EventId == id)
            .Select(ToResponse).SingleOrDefaultAsync();
        if (ev is null) return Fail(404, "Event not found");
        return ev;
    }

    /// <summary>Create a new event.</summary>
    /// <remarks>Organiser only. The event date must be in the future. The logged-in Organiser becomes the owner.</remarks>
    /// <response code="201">Event created.</response>
    /// <response code="400">Validation failed or the date is not in the future.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in as a Participant.</response>
    [HttpPost]
    [SessionAuthorize(RoleNames.Organiser)]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<EventResponse>> Create([FromBody] EventRequest request)
    {
        if (request.EventDate!.Value <= DateTime.UtcNow)
            return Fail(400, "Invalid event date", "New events must be scheduled in the future.");

        var ev = new Event
        {
            OrganiserId = CurrentUserId,
            Name = request.Name.Trim(),
            Description = request.Description.Trim(),
            EventDate = request.EventDate.Value,
            Location = request.Location.Trim(),
            DistanceKm = request.DistanceKm,
            EventType = request.EventType!.Value
        };
        _db.Events.Add(ev);
        await _db.SaveChangesAsync();

        var dto = await _db.Events.AsNoTracking().Where(e => e.EventId == ev.EventId)
            .Select(ToResponse).SingleAsync();
        return CreatedAtAction(nameof(GetById), new { id = ev.EventId }, dto);
    }

    /// <summary>Update an event you own.</summary>
    /// <remarks>Organiser only, and only for events they created.</remarks>
    /// <response code="200">Event updated.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or the event belongs to another Organiser.</response>
    /// <response code="404">Event does not exist.</response>
    [HttpPut("{id:int}")]
    [SessionAuthorize(RoleNames.Organiser)]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventResponse>> Update(int id, [FromBody] EventRequest request)
    {
        var ev = await _db.Events.SingleOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return Fail(404, "Event not found");
        if (ev.OrganiserId != CurrentUserId)
            return Fail(403, "Access denied", "You can only change events that you created.");

        ev.Name = request.Name.Trim();
        ev.Description = request.Description.Trim();
        ev.EventDate = request.EventDate!.Value;
        ev.Location = request.Location.Trim();
        ev.DistanceKm = request.DistanceKm;
        ev.EventType = request.EventType!.Value;
        await _db.SaveChangesAsync();

        return await _db.Events.AsNoTracking().Where(e => e.EventId == id).Select(ToResponse).SingleAsync();
    }

    /// <summary>Delete an event you own (also removes its categories, enrolments and results).</summary>
    /// <remarks>Organiser only, and only for events they created.</remarks>
    /// <response code="204">Event deleted.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or the event belongs to another Organiser.</response>
    /// <response code="404">Event does not exist.</response>
    [HttpDelete("{id:int}")]
    [SessionAuthorize(RoleNames.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var ev = await _db.Events
            .Include(e => e.Categories)
            .Include(e => e.Enrolments).ThenInclude(en => en.Result)
            .SingleOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return Fail(404, "Event not found");
        if (ev.OrganiserId != CurrentUserId)
            return Fail(403, "Access denied", "You can only delete events that you created.");

        _db.Events.Remove(ev);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
