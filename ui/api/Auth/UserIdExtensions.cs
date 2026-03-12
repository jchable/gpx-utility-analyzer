namespace GpxAnalyzer.Api.Auth;

using System.Security.Claims;

public static class UserIdExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user)
        => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)
           ?? throw new UnauthorizedAccessException("No user ID in claims"));
}
