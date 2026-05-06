using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace CadastraAI.API.Auth;

public static class CurrentUserAccessor
{
    public static Guid? UserId(this ClaimsPrincipal principal)
    {
        var sub = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
                  ?? principal.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    public static string? Email(this ClaimsPrincipal principal)
    {
        return principal.FindFirstValue(ClaimTypes.Email)
               ?? principal.FindFirstValue(JwtRegisteredClaimNames.Email);
    }
}
