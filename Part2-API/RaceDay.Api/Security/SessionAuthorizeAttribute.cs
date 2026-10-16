using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace RaceDay.Api.Security;

public static class SessionKeys
{
    public const string UserId = "UserId";
    public const string Role = "Role";
}

/// <summary>
/// Session-based authorisation. A request is "authenticated" when the server-side session
/// holds a UserId + Role (set at login). Optionally restricts the endpoint to ONE role.
/// 401 = not logged in, 403 = logged in but wrong role.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class SessionAuthorizeAttribute : Attribute, IAuthorizationFilter
{
    public string? Role { get; }

    public SessionAuthorizeAttribute() { }
    public SessionAuthorizeAttribute(string role) { Role = role; }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var session = context.HttpContext.Session;
        var userId = session.GetInt32(SessionKeys.UserId);
        var role = session.GetString(SessionKeys.Role);

        if (userId is null || role is null)
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Authentication required",
                Detail = "You must log in before accessing this resource."
            })
            { StatusCode = StatusCodes.Status401Unauthorized };
            return;
        }

        if (Role is not null && !string.Equals(role, Role, StringComparison.Ordinal))
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Access denied",
                Detail = $"This action is only available to the {Role} role."
            })
            { StatusCode = StatusCodes.Status403Forbidden };
        }
    }
}
