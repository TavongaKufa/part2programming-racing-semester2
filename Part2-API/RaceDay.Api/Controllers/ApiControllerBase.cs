using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>Id of the logged-in user (from the session). 0 if nobody is logged in.</summary>
    protected int CurrentUserId => HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0;

    protected ObjectResult Fail(int status, string title, string? detail = null) =>
        StatusCode(status, new ProblemDetails { Status = status, Title = title, Detail = detail });
}
