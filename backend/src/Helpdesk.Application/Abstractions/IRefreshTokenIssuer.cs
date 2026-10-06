using Helpdesk.Domain.Entities;

namespace Helpdesk.Application.Abstractions;

public interface IRefreshTokenIssuer
{
    IssuedRefreshToken Issue(Guid userId, DateTimeOffset now);

    string Hash(string token);
}

/// <summary>
/// <paramref name="Token"/> is returned to the client once; only <paramref name="Entity"/>, which holds its hash, is stored.
/// </summary>
public sealed record IssuedRefreshToken(RefreshToken Entity, string Token);
