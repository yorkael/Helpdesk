using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Helpdesk.Application.Abstractions;
using Helpdesk.Domain.Entities;

namespace Helpdesk.Infrastructure.Authentication;

internal sealed class RefreshTokenIssuer(JwtOptions options) : IRefreshTokenIssuer
{
    private const int TokenSizeInBytes = 32;

    public IssuedRefreshToken Issue(Guid userId, DateTimeOffset now)
    {
        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenSizeInBytes));
        var entity = new RefreshToken(userId, Hash(token), now, now + options.RefreshTokenLifetime);

        return new IssuedRefreshToken(entity, token);
    }

    // A fast unsalted hash is enough: the token is 256 random bits, so it cannot be guessed,
    // and a deterministic hash lets the database find it through a unique index.
    public string Hash(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
