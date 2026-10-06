using System.Security.Cryptography;
using Helpdesk.Infrastructure;
using Helpdesk.Infrastructure.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Helpdesk.Tests.Infrastructure.Authentication;

public class JwtOptionsTests
{
    [Fact]
    public void Valid_configuration_is_read()
    {
        var options = JwtOptions.FromConfiguration(CreateConfiguration());

        Assert.Equal("helpdesk-tests", options.Issuer);
        Assert.Equal("helpdesk-tests-client", options.Audience);
        Assert.Equal(TimeSpan.FromMinutes(15), options.AccessTokenLifetime);
        Assert.Equal(TimeSpan.FromDays(7), options.RefreshTokenLifetime);
        Assert.Equal(Convert.FromBase64String(TestConfiguration.SigningKey), options.SigningKey.Key);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Missing_signing_key_explains_how_to_set_it(string? signingKey)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            JwtOptions.FromConfiguration(CreateConfiguration(("Jwt:SigningKey", signingKey))));

        Assert.Contains("'Jwt:SigningKey' is not configured", exception.Message);
        Assert.Contains("dotnet user-secrets", exception.Message);
        Assert.Contains("Jwt__SigningKey", exception.Message);
    }

    [Fact]
    public void Signing_key_that_is_not_base64_is_rejected()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            JwtOptions.FromConfiguration(CreateConfiguration(("Jwt:SigningKey", "not base64 at all!"))));

        Assert.Contains("not valid base64", exception.Message);
    }

    [Fact]
    public void Signing_key_shorter_than_256_bits_is_rejected_without_revealing_it()
    {
        var shortKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(31));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            JwtOptions.FromConfiguration(CreateConfiguration(("Jwt:SigningKey", shortKey))));

        Assert.Contains("at least 32 bytes", exception.Message);
        Assert.DoesNotContain(shortKey, exception.Message);
    }

    [Fact]
    public void Signing_key_of_exactly_256_bits_is_accepted()
    {
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        var options = JwtOptions.FromConfiguration(CreateConfiguration(("Jwt:SigningKey", key)));

        Assert.Equal(32, options.SigningKey.Key.Length);
    }

    [Fact]
    public void Every_invalid_setting_is_reported_at_once()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => JwtOptions.FromConfiguration(CreateConfiguration(
            ("Jwt:Issuer", null),
            ("Jwt:Audience", " "),
            ("Jwt:AccessTokenMinutes", "0"),
            ("Jwt:RefreshTokenDays", "seven"))));

        Assert.Contains("'Jwt:Issuer'", exception.Message);
        Assert.Contains("'Jwt:Audience'", exception.Message);
        Assert.Contains("'Jwt:AccessTokenMinutes'", exception.Message);
        Assert.Contains("'Jwt:RefreshTokenDays'", exception.Message);
    }

    [Fact]
    public void Infrastructure_registration_fails_fast_without_a_signing_key()
    {
        var configuration = CreateConfiguration(("Jwt:SigningKey", null));

        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddInfrastructure(configuration));
    }

    private static IConfiguration CreateConfiguration(
        params (string Key, string? Value)[] overrides)
    {
        var settings = TestConfiguration.CreateSettings(TestConfiguration.UnusedConnectionString);
        foreach (var (key, value) in overrides)
        {
            settings[key] = value;
        }

        return TestConfiguration.CreateConfiguration(settings);
    }
}
