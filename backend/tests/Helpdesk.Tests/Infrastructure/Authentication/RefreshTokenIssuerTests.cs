using System.Buffers.Text;
using Helpdesk.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Helpdesk.Tests.Infrastructure.Authentication;

public class RefreshTokenIssuerTests
{
    private static readonly DateTimeOffset UtcNow = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

    private readonly IRefreshTokenIssuer _issuer =
        TestConfiguration.CreateInfrastructureServices().GetRequiredService<IRefreshTokenIssuer>();

    [Fact]
    public void Token_is_256_random_bits_in_base64url()
    {
        var issued = _issuer.Issue(Guid.NewGuid(), UtcNow);

        Assert.Equal(32, Base64Url.DecodeFromChars(issued.Token).Length);
        Assert.DoesNotContain('+', issued.Token);
        Assert.DoesNotContain('/', issued.Token);
        Assert.DoesNotContain('=', issued.Token);
    }

    [Fact]
    public void Each_token_is_different()
    {
        var userId = Guid.NewGuid();

        Assert.NotEqual(_issuer.Issue(userId, UtcNow).Token, _issuer.Issue(userId, UtcNow).Token);
    }

    [Fact]
    public void Entity_stores_the_sha256_hash_and_expires_after_7_days()
    {
        var userId = Guid.NewGuid();

        var issued = _issuer.Issue(userId, UtcNow);

        Assert.Equal(userId, issued.Entity.UserId);
        Assert.Equal(_issuer.Hash(issued.Token), issued.Entity.TokenHash);
        Assert.NotEqual(issued.Token, issued.Entity.TokenHash);
        Assert.Equal(UtcNow, issued.Entity.CreatedAt);
        Assert.Equal(UtcNow.AddDays(7), issued.Entity.ExpiresAt);
    }

    [Fact]
    public void Hash_is_deterministic_lowercase_hex_sha256()
    {
        // SHA-256 of "abc", from the FIPS 180-2 test vectors.
        Assert.Equal(
            "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad",
            _issuer.Hash("abc"));
        Assert.Equal(_issuer.Hash("token"), _issuer.Hash("token"));
    }
}
