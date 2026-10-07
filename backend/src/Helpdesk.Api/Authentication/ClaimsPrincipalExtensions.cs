using System.Security.Claims;
using Helpdesk.Domain.Enums;
using Helpdesk.Infrastructure.Authentication;

namespace Helpdesk.Api.Authentication;

public static class ClaimsPrincipalExtensions
{
    /// <summary>The id from the validated access token's subject claim; only call it on authenticated endpoints.</summary>
    public static Guid GetUserId(this ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue(AccessTokenClaimTypes.Subject)!);

    /// <summary>The role from the validated access token; only call it on authenticated endpoints.</summary>
    /// <exception cref="InvalidOperationException">
    /// The token does not carry exactly one role claim with an exact role name. There is no default role,
    /// because guessing one could show a user tickets that are not theirs.
    /// </exception>
    public static UserRole GetUserRole(this ClaimsPrincipal user)
    {
        var roles = user.FindAll(AccessTokenClaimTypes.Role).Select(claim => claim.Value).ToList();

        // Compared by name: Enum.TryParse would also accept "0" or "Admin,Client".
        if (roles is not [var role] || !Enum.GetNames<UserRole>().Contains(role))
        {
            throw new InvalidOperationException("The access token does not carry exactly one valid role.");
        }

        return Enum.Parse<UserRole>(role);
    }
}
