using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

[Route("api/auth")]
public class AuthController(AppDbContext db, PasswordHasher hasher, IConfiguration config) : ApiControllerBase
{
    /// <summary>Register a new account as an Organiser or a Participant.</summary>
    /// <remarks>
    /// Participants can register freely. To stop anyone from making themselves an Organiser,
    /// registering with the role "Organiser" also requires the invite code (organiserCode) issued by RaceDay.
    /// On success the user is also signed in (a session cookie is issued).
    /// </remarks>
    /// <response code="201">Account created. Returns the user (never the password).</response>
    /// <response code="400">Validation errors, or a missing/invalid organiser code.</response>
    /// <response code="409">The email address is already registered.</response>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserResponse>> Register(RegisterRequest request)
    {
        if (request.Role == Roles.Organiser)
        {
            var expected = config["Registration:OrganiserCode"];
            if (string.IsNullOrEmpty(expected) || request.OrganiserCode != expected)
            {
                ModelState.AddModelError(nameof(request.OrganiserCode), "A valid organiser invite code is required to register as an Organiser.");
                return ValidationProblem(ModelState);
            }
        }

        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email))
            return ConflictProblem("That email address is already in use.");

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            PasswordHash = hasher.Hash(request.Password),
            Role = request.Role,
            PhoneNumber = request.PhoneNumber
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        await SignInAsync(user);
        return Created("/api/users/me", UserResponse.From(user));
    }

    /// <summary>Log in with email and password.</summary>
    /// <remarks>
    /// On success a session cookie ("RaceDay.Session") is issued that holds the user's id and role.
    /// The browser (including Swagger UI) sends it automatically on all following requests.
    /// </remarks>
    /// <response code="200">Logged in. Returns the user id, name and role.</response>
    /// <response code="401">Invalid email or password.</response>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user is null || !hasher.Verify(request.Password, user.PasswordHash))
        {
            return StatusCode(StatusCodes.Status401Unauthorized,
                new ProblemDetails { Status = 401, Title = "Unauthorized", Detail = "Invalid email or password." });
        }

        await SignInAsync(user);
        return Ok(new LoginResponse { UserId = user.UserId, FullName = user.FullName, Role = user.Role });
    }

    /// <summary>Log out and end the current session.</summary>
    /// <response code="204">Session ended.</response>
    /// <response code="401">Not logged in.</response>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    private async Task SignInAsync(User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    }
}
