using Helpdesk.Domain.Entities;

namespace Helpdesk.Tests.Domain;

public class RefreshTokenTests
{
    private static readonly DateTimeOffset UtcNow = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ExpiresAt = UtcNow.AddDays(7);

    [Fact]
    public void New_token_is_active_until_it_expires()
    {
        var token = new RefreshToken(Guid.NewGuid(), "hash", UtcNow, ExpiresAt);

        Assert.True(token.IsActive(UtcNow));
        Assert.True(token.IsActive(ExpiresAt.AddTicks(-1)));
        Assert.False(token.IsActive(ExpiresAt));
    }

    [Fact]
    public void Empty_user_id_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new RefreshToken(Guid.Empty, "hash", UtcNow, ExpiresAt));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_token_hash_is_rejected(string tokenHash)
    {
        Assert.Throws<ArgumentException>(() => new RefreshToken(Guid.NewGuid(), tokenHash, UtcNow, ExpiresAt));
    }

    [Fact]
    public void Non_utc_timestamps_are_rejected()
    {
        var localTime = new DateTimeOffset(2026, 10, 6, 7, 0, 0, TimeSpan.FromHours(-5));

        Assert.Throws<ArgumentException>(() => new RefreshToken(Guid.NewGuid(), "hash", localTime, ExpiresAt));
        Assert.Throws<ArgumentException>(() => new RefreshToken(Guid.NewGuid(), "hash", UtcNow, localTime.AddDays(7)));
    }

    [Fact]
    public void Expiration_not_after_creation_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new RefreshToken(Guid.NewGuid(), "hash", UtcNow, UtcNow));
    }

    [Fact]
    public void Revoked_token_is_no_longer_active_and_records_its_replacement()
    {
        var token = new RefreshToken(Guid.NewGuid(), "hash", UtcNow, ExpiresAt);
        var replacementId = Guid.CreateVersion7();

        token.Revoke(UtcNow.AddMinutes(5), replacementId);

        Assert.False(token.IsActive(UtcNow.AddMinutes(6)));
        Assert.Equal(UtcNow.AddMinutes(5), token.RevokedAt);
        Assert.Equal(replacementId, token.ReplacedByTokenId);
    }

    [Fact]
    public void Revoking_without_replacement_leaves_it_empty()
    {
        var token = new RefreshToken(Guid.NewGuid(), "hash", UtcNow, ExpiresAt);

        token.Revoke(UtcNow);

        Assert.NotNull(token.RevokedAt);
        Assert.Null(token.ReplacedByTokenId);
    }

    [Fact]
    public void Revoking_twice_is_rejected()
    {
        var token = new RefreshToken(Guid.NewGuid(), "hash", UtcNow, ExpiresAt);
        token.Revoke(UtcNow);

        Assert.Throws<InvalidOperationException>(() => token.Revoke(UtcNow));
    }

    [Fact]
    public void Invalid_revocation_leaves_the_token_active()
    {
        var token = new RefreshToken(Guid.NewGuid(), "hash", UtcNow, ExpiresAt);

        Assert.Throws<ArgumentException>(() => token.Revoke(UtcNow, Guid.Empty));
        Assert.True(token.IsActive(UtcNow));
    }
}
