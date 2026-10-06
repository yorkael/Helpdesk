namespace Helpdesk.Domain.Entities;

public class RefreshToken
{
    public RefreshToken(Guid userId, string tokenHash, DateTimeOffset createdAt, DateTimeOffset expiresAt)
    {
        Id = Guid.CreateVersion7();
        UserId = Guard.NotEmpty(userId, nameof(userId));
        TokenHash = Guard.NotBlank(tokenHash, nameof(tokenHash));
        CreatedAt = Guard.Utc(createdAt, nameof(createdAt));
        ExpiresAt = Guard.Utc(expiresAt, nameof(expiresAt));

        if (ExpiresAt <= CreatedAt)
        {
            throw new ArgumentException("Expiration must be later than creation.", nameof(expiresAt));
        }
    }

    private RefreshToken()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public Guid? ReplacedByTokenId { get; private set; }

    // Optimistic concurrency token; persistence maps it to PostgreSQL's xmin system column.
    public uint Version { get; private set; }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && now < ExpiresAt;

    public void Revoke(DateTimeOffset now, Guid? replacedByTokenId = null)
    {
        if (RevokedAt is not null)
        {
            throw new InvalidOperationException("Refresh token is already revoked.");
        }

        var revokedAt = Guard.Utc(now, nameof(now));
        if (replacedByTokenId is { } replacementId)
        {
            Guard.NotEmpty(replacementId, nameof(replacedByTokenId));
        }

        RevokedAt = revokedAt;
        ReplacedByTokenId = replacedByTokenId;
    }
}
