using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;

/// <summary>Age / distance categories for an event.</summary>
[Route("api")]
[SessionAuthorize]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
public class CategoriesController : ApiControllerBase
{
    private readonly AppDbContext _db;

    public CategoriesController(AppDbContext db) => _db = db;

    /// <summary>List the categories of an event.</summary>
    /// <remarks>Any logged-in user.</remarks>
    /// <response code="200">The event's categories.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="404">Event does not exist.</response>
    [HttpGet("events/{eventId:int}/categories")]
    [ProducesResponseType(typeof(List<CategoryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<CategoryResponse>>> GetForEvent(int eventId)
    {
        if (!await _db.Events.AnyAsync(e => e.EventId == eventId)) return Fail(404, "Event not found");

        return await _db.Categories.AsNoTracking()
            .Where(c => c.EventId == eventId)
            .OrderBy(c => c.Name)
            .Select(c => new CategoryResponse(c.CategoryId, c.EventId, c.Name, c.CategoryType))
            .ToListAsync();
    }

    /// <summary>Add a category (e.g. "Under 20", "Senior", "10km") to an event you own.</summary>
    /// <remarks>Organiser only, and only for their own events. Category names are unique per event.</remarks>
    /// <response code="201">Category created.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or the event belongs to another Organiser.</response>
    /// <response code="404">Event does not exist.</response>
    /// <response code="409">This event already has a category with that name.</response>
    [HttpPost("events/{eventId:int}/categories")]
    [SessionAuthorize(RoleNames.Organiser)]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategoryResponse>> Create(int eventId, [FromBody] CategoryRequest request)
    {
        var ev = await _db.Events.AsNoTracking().SingleOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null) return Fail(404, "Event not found");
        if (ev.OrganiserId != CurrentUserId)
            return Fail(403, "Access denied", "You can only add categories to events that you created.");

        var name = request.Name.Trim();
        if (await _db.Categories.AnyAsync(c => c.EventId == eventId && c.Name == name))
            return Fail(409, "Category already exists", "This event already has a category with that name.");

        var category = new Category { EventId = eventId, Name = name, CategoryType = request.CategoryType!.Value };
        _db.Categories.Add(category);
        await _db.SaveChangesAsync();

        var dto = new CategoryResponse(category.CategoryId, category.EventId, category.Name, category.CategoryType);
        return Created($"/api/events/{eventId}/categories", dto);
    }

    /// <summary>Rename / change a category of an event you own.</summary>
    /// <remarks>Organiser only, and only for their own events.</remarks>
    /// <response code="200">Category updated.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or the event belongs to another Organiser.</response>
    /// <response code="404">Category does not exist.</response>
    /// <response code="409">Another category on this event already has that name.</response>
    [HttpPut("categories/{id:int}")]
    [SessionAuthorize(RoleNames.Organiser)]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategoryResponse>> Update(int id, [FromBody] CategoryRequest request)
    {
        var category = await _db.Categories.Include(c => c.Event).SingleOrDefaultAsync(c => c.CategoryId == id);
        if (category is null) return Fail(404, "Category not found");
        if (category.Event.OrganiserId != CurrentUserId)
            return Fail(403, "Access denied", "You can only change categories of events that you created.");

        var name = request.Name.Trim();
        if (await _db.Categories.AnyAsync(c => c.EventId == category.EventId && c.Name == name && c.CategoryId != id))
            return Fail(409, "Category already exists", "This event already has a category with that name.");

        category.Name = name;
        category.CategoryType = request.CategoryType!.Value;
        await _db.SaveChangesAsync();

        return new CategoryResponse(category.CategoryId, category.EventId, category.Name, category.CategoryType);
    }

    /// <summary>Delete a category of an event you own.</summary>
    /// <remarks>Organiser only. A category that already has enrolments cannot be deleted.</remarks>
    /// <response code="204">Category deleted.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or the event belongs to another Organiser.</response>
    /// <response code="404">Category does not exist.</response>
    /// <response code="409">Participants are already enrolled in this category.</response>
    [HttpDelete("categories/{id:int}")]
    [SessionAuthorize(RoleNames.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id)
    {
        var category = await _db.Categories.Include(c => c.Event).SingleOrDefaultAsync(c => c.CategoryId == id);
        if (category is null) return Fail(404, "Category not found");
        if (category.Event.OrganiserId != CurrentUserId)
            return Fail(403, "Access denied", "You can only delete categories of events that you created.");
        if (await _db.Enrolments.AnyAsync(en => en.CategoryId == id))
            return Fail(409, "Category in use", "Participants are already enrolled in this category.");

        _db.Categories.Remove(category);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
