using System.Security.Cryptography;
using Helpdesk.Application.Abstractions;
using Helpdesk.Domain.Entities;
using Helpdesk.Domain.Enums;
using Helpdesk.Infrastructure.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Helpdesk.Tests.Infrastructure.Authentication;

public class AccessTokenIssuerTests
{
    private readonly IAccessTokenIssuer _issuer;
    private readonly JwtOptions _options;
    private readonly User _user = new("Luis Vega", "luis@example.com", "hash", UserRole.Agent);

    // Whole seconds, because a JWT stores times with second precision; real time, because validation checks the clock.
    private readonly DateTimeOffset _now = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

    public AccessTokenIssuerTests()
    {
        var services = TestConfiguration.CreateInfrastructureServices();
        _issuer = services.GetRequiredService<IAccessTokenIssuer>();
        _options = services.GetRequiredService<JwtOptions>();
    }

    [Fact]
    public async Task Token_validates_and_carries_the_user_claims_including_the_role()
    {
        var accessToken = _issuer.Issue(_user, _now);

        var result = await ValidateAsync(accessToken.Value, _options.SigningKey);

        Assert.True(result.IsValid, result.Exception?.Message);
        Assert.Equal(_user.Id.ToString(), result.Claims[AccessTokenClaimTypes.Subject]);
        Assert.Equal("luis@example.com", result.Claims[AccessTokenClaimTypes.Email]);
        Assert.Equal("Luis Vega", result.Claims[AccessTokenClaimTypes.Name]);
        Assert.Equal("Agent", result.Claims[AccessTokenClaimTypes.Role]);
        Assert.True(Guid.TryParse((string)result.Claims[AccessTokenClaimTypes.TokenId], out _));
    }

    [Fact]
    public void Token_is_signed_with_hs256_and_expires_after_15_minutes()
    {
        var accessToken = _issuer.Issue(_user, _now);
        var jwt = new JsonWebToken(accessToken.Value);

        Assert.Equal(SecurityAlgorithms.HmacSha256, jwt.Alg);
        Assert.Equal(_now.AddMinutes(15), accessToken.ExpiresAt);
        Assert.Equal(accessToken.ExpiresAt.UtcDateTime, jwt.ValidTo);
        Assert.Equal(_now.UtcDateTime, jwt.IssuedAt);
        Assert.Equal("helpdesk-tests", jwt.Issuer);
        Assert.Equal(["helpdesk-tests-client"], jwt.Audiences);
    }

    [Fact]
    public async Task Token_signed_with_another_key_is_rejected()
    {
        var accessToken = _issuer.Issue(_user, _now);

        var result = await ValidateAsync(accessToken.Value, new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32)));

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Expired_token_is_rejected()
    {
        var accessToken = _issuer.Issue(_user, _now.AddMinutes(-16));

        var result = await ValidateAsync(accessToken.Value, _options.SigningKey);

        Assert.False(result.IsValid);
        Assert.IsType<SecurityTokenExpiredException>(result.Exception);
    }

    [Fact]
    public void Each_token_has_its_own_id()
    {
        var first = new JsonWebToken(_issuer.Issue(_user, _now).Value);
        var second = new JsonWebToken(_issuer.Issue(_user, _now).Value);

        Assert.NotEqual(first.Id, second.Id);
    }

    private Task<TokenValidationResult> ValidateAsync(string token, SecurityKey signingKey) =>
        new JsonWebTokenHandler().ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = _options.Issuer,
            ValidAudience = _options.Audience,
            IssuerSigningKey = signingKey,
            ValidAlgorithms = [JwtOptions.SigningAlgorithm],
            ClockSkew = TimeSpan.Zero
        });
}
