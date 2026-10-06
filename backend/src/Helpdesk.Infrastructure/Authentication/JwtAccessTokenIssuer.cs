using Helpdesk.Application.Abstractions;
using Helpdesk.Domain.Entities;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Helpdesk.Infrastructure.Authentication;

internal sealed class JwtAccessTokenIssuer(JwtOptions options) : IAccessTokenIssuer
{
    private readonly JsonWebTokenHandler _tokenHandler = new();
    private readonly SigningCredentials _signingCredentials = new(options.SigningKey, JwtOptions.SigningAlgorithm);

    public AccessToken Issue(User user, DateTimeOffset now)
    {
        var expiresAt = now + options.AccessTokenLifetime;

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = options.Issuer,
            Audience = options.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = _signingCredentials,
            Claims = new Dictionary<string, object>
            {
                [AccessTokenClaimTypes.Subject] = user.Id.ToString(),
                [AccessTokenClaimTypes.Email] = user.Email,
                [AccessTokenClaimTypes.Name] = user.Name,
                [AccessTokenClaimTypes.Role] = user.Role.ToString(),
                [AccessTokenClaimTypes.TokenId] = Guid.NewGuid().ToString()
            }
        };

        return new AccessToken(_tokenHandler.CreateToken(descriptor), expiresAt);
    }
}
