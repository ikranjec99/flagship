using System.Security.Claims;

namespace Flagship.API.Helpers;

public static class ClaimsPrincipalExtensions
{
    public static string Actor(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.Email) ?? "unknown";
}
