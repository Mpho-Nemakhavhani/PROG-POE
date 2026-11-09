using System.Security.Claims;

namespace RaceDay.Api.Services;

public static class ClaimsExtensions
{
    /// <summary>The id of the logged-in user, read from the session cookie's claims.</summary>
    public static int GetUserId(this ClaimsPrincipal user) =>
        int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
