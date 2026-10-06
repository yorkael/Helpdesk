using Microsoft.IdentityModel.JsonWebTokens;

namespace Helpdesk.Infrastructure.Authentication;

/// <summary>
/// Short JWT claim names, kept as issued; the API reads them without mapping them to WS-* URIs.
/// </summary>
public static class AccessTokenClaimTypes
{
    public const string Subject = JwtRegisteredClaimNames.Sub;
    public const string Email = JwtRegisteredClaimNames.Email;
    public const string Name = JwtRegisteredClaimNames.Name;
    public const string Role = "role";
    public const string TokenId = JwtRegisteredClaimNames.Jti;
}
