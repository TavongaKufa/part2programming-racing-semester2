using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;

/// <summary>Registration, login and logout (session based).</summary>
[Route("api/auth")]
public class AuthController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher<User> _hasher;

    public AuthController(AppDbContext db, IPasswordHasher<User> hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    /// <summary>Register a new account as an Organiser or a Participant.</summary>
    /// <remarks>Public endpoint. The password is hashed before it is stored.</remarks>
    /// <response code="201">Account created. Returns the new profile.</response>
    /// <response code="400">Validation failed (missing fields, weak password, unknown role).</response>
    /// <response code="409">An account with this email already exists.</response>
    [HttpPost("register")]
    [ProducesResponseType(typeof(ProfileResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var roleName = RoleNames.All.FirstOrDefault(r =>
            string.Equals(r, request.Role, StringComparison.OrdinalIgnoreCase));
        if (roleName is null)
            return Fail(400, "Invalid role", "Role must be either 'Organiser' or 'Participant'.");

        var email = request.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email))
            return Fail(409, "Email already registered", "An account with this email address already exists.");

        var role = await _db.Roles.SingleAsync(r => r.RoleName == roleName);
        var user = new User
        {
            RoleId = role.RoleId,
            Role = role,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = email,
            PhoneNumber = request.PhoneNumber
        };
        user.PasswordHash = _hasher.HashPassword(user, request.Password);

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return Created("/api/profile", ProfileResponse.From(user));
    }

    /// <summary>Log in with email and password. Starts a server-side session.</summary>
    /// <remarks>Public endpoint. On success a session cookie is issued; it stores the user id and role.</remarks>
    /// <response code="200">Logged in. Returns the user's profile including their role.</response>
    /// <response code="401">Email or password is incorrect.</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(ProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ProfileResponse>> Login([FromBody] LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.Include(u => u.Role).SingleOrDefaultAsync(u => u.Email == email);

        if (user is null ||
            _hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
        {
            return Fail(401, "Invalid credentials", "The email or password is incorrect.");
        }

        HttpContext.Session.Clear();
        HttpContext.Session.SetInt32(SessionKeys.UserId, user.UserId);
        HttpContext.Session.SetString(SessionKeys.Role, user.Role.RoleName);

        return ProfileResponse.From(user);
    }

    /// <summary>Log out and end the current session.</summary>
    /// <remarks>Any logged-in user.</remarks>
    /// <response code="200">Session ended.</response>
    /// <response code="401">Not logged in.</response>
    [HttpPost("logout")]
    [SessionAuthorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return Ok(new { message = "You have been logged out." });
    }
}
