using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;

/// <summary>View and update the logged-in user's own profile.</summary>
[Route("api/profile")]
[SessionAuthorize]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
public class ProfileController : ApiControllerBase
{
    private readonly AppDbContext _db;

    public ProfileController(AppDbContext db) => _db = db;

    /// <summary>Get the logged-in user's profile.</summary>
    /// <remarks>Any logged-in user (Organiser or Participant).</remarks>
    /// <response code="200">The user's profile.</response>
    /// <response code="401">Not logged in.</response>
    [HttpGet]
    [ProducesResponseType(typeof(ProfileResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ProfileResponse>> Get()
    {
        var user = await _db.Users.AsNoTracking().Include(u => u.Role)
            .SingleOrDefaultAsync(u => u.UserId == CurrentUserId);
        if (user is null) return Fail(401, "Authentication required", "Your account no longer exists.");
        return ProfileResponse.From(user);
    }

    /// <summary>Update the logged-in user's first name, last name and phone number.</summary>
    /// <remarks>Any logged-in user. Email and role cannot be changed.</remarks>
    /// <response code="200">Profile updated. Returns the new profile.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not logged in.</response>
    [HttpPut]
    [ProducesResponseType(typeof(ProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProfileResponse>> Update([FromBody] UpdateProfileRequest request)
    {
        var user = await _db.Users.Include(u => u.Role).SingleOrDefaultAsync(u => u.UserId == CurrentUserId);
        if (user is null) return Fail(401, "Authentication required", "Your account no longer exists.");

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.PhoneNumber = request.PhoneNumber;
        await _db.SaveChangesAsync();

        return ProfileResponse.From(user);
    }
}
