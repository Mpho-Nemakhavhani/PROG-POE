using Microsoft.AspNetCore.Mvc;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

[ApiController]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>Id of the logged-in user (from the session cookie).</summary>
    protected int CurrentUserId => User.GetUserId();

    protected ObjectResult ForbiddenProblem(string detail) =>
        StatusCode(StatusCodes.Status403Forbidden,
            new ProblemDetails { Status = StatusCodes.Status403Forbidden, Title = "Forbidden", Detail = detail });

    protected ObjectResult BadRequestProblem(string detail) =>
        StatusCode(StatusCodes.Status400BadRequest,
            new ProblemDetails { Status = StatusCodes.Status400BadRequest, Title = "Bad Request", Detail = detail });

    protected ObjectResult ConflictProblem(string detail) =>
        StatusCode(StatusCodes.Status409Conflict,
            new ProblemDetails { Status = StatusCodes.Status409Conflict, Title = "Conflict", Detail = detail });
}
