using System.Security.Claims;

namespace PhoneGrapher.Api.Controllers;

internal static class ControllerExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub")
            ?? throw new UnauthorizedAccessException("Missing user id claim.");

        return Guid.Parse(value);
    }
}
