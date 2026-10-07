using System.Security.Claims;
using Helpdesk.Infrastructure.Authentication;

namespace Helpdesk.Api.Authentication;

public static class ClaimsPrincipalExtensions
{
    /// <summary>The id from the validated access token's subject claim; only call it on authenticated endpoints.</summary>
    public static Guid GetUserId(this ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue(AccessTokenClaimTypes.Subject)!);
}
