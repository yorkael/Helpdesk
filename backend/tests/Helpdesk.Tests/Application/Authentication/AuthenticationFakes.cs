using Helpdesk.Application.Abstractions;
using Helpdesk.Domain.Entities;

namespace Helpdesk.Tests.Application.Authentication;

internal sealed class InMemoryUserRepository : IUserRepository
{
    public List<User> Users { get; } = [];

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Users.SingleOrDefault(user => user.Id == id));

    public Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        Task.FromResult(Users.SingleOrDefault(user => user.Email == normalizedEmail));

    public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        Task.FromResult(Users.Any(user => user.Email == normalizedEmail));

    /// <summary>The ids of each names lookup, so tests can count lookups and check what was asked for.</summary>
    public List<IReadOnlyCollection<Guid>> NameLookups { get; } = [];

    public Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken)
    {
        NameLookups.Add(ids);
        return Task.FromResult<IReadOnlyDictionary<Guid, string>>(
            Users.Where(user => ids.Contains(user.Id)).ToDictionary(user => user.Id, user => user.Name));
    }

    public void Add(User user) => Users.Add(user);
}

internal sealed class InMemoryRefreshTokenRepository : IRefreshTokenRepository
{
    public List<RefreshToken> Tokens { get; } = [];

    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        Task.FromResult(Tokens.SingleOrDefault(token => token.TokenHash == tokenHash));

    public Task<IReadOnlyList<RefreshToken>> GetActiveByUserAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<RefreshToken>>(
            Tokens.Where(token => token.UserId == userId && token.IsActive(now)).ToList());

    public void Add(RefreshToken refreshToken) => Tokens.Add(refreshToken);
}

/// <summary>
/// Hashes carry a version, like the iteration count in a real hash, so tests can trigger a rehash by bumping it.
/// </summary>
internal sealed class FakePasswordHasher : IPasswordHasher
{
    public int CurrentVersion { get; set; } = 1;

    public int SimulatedVerifications { get; private set; }

    public string Hash(string password) => HashWithVersion(password, CurrentVersion);

    public PasswordVerification Verify(string passwordHash, string password)
    {
        if (passwordHash == Hash(password))
        {
            return PasswordVerification.Success;
        }

        return Enumerable.Range(1, CurrentVersion - 1).Any(version => passwordHash == HashWithVersion(password, version))
            ? PasswordVerification.SuccessRehashNeeded
            : PasswordVerification.Failed;
    }

    public void SimulateVerification(string password) => SimulatedVerifications++;

    private static string HashWithVersion(string password, int version) => $"hashed-v{version}:{password}";
}

internal sealed class FakeAccessTokenIssuer : IAccessTokenIssuer
{
    public AccessToken Issue(User user, DateTimeOffset now) =>
        new($"access:{user.Id}:{user.Role}", now.AddMinutes(15));
}

internal sealed class FakeRefreshTokenIssuer : IRefreshTokenIssuer
{
    private int _issuedCount;

    public IssuedRefreshToken Issue(Guid userId, DateTimeOffset now)
    {
        var token = $"refresh-{++_issuedCount}";
        return new IssuedRefreshToken(new RefreshToken(userId, Hash(token), now, now.AddDays(7)), token);
    }

    public string Hash(string token) => $"hash:{token}";
}
