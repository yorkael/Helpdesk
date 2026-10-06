using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Helpdesk.Infrastructure.Authentication;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public const string SigningAlgorithm = SecurityAlgorithms.HmacSha256;

    // RFC 7518 section 3.2: an HS256 key must be at least as long as the hash output (256 bits).
    private const int MinimumSigningKeyBytes = 32;

    private JwtOptions(
        string issuer,
        string audience,
        SymmetricSecurityKey signingKey,
        TimeSpan accessTokenLifetime,
        TimeSpan refreshTokenLifetime)
    {
        Issuer = issuer;
        Audience = audience;
        SigningKey = signingKey;
        AccessTokenLifetime = accessTokenLifetime;
        RefreshTokenLifetime = refreshTokenLifetime;
    }

    public string Issuer { get; }

    public string Audience { get; }

    public SymmetricSecurityKey SigningKey { get; }

    public TimeSpan AccessTokenLifetime { get; }

    public TimeSpan RefreshTokenLifetime { get; }

    /// <exception cref="InvalidOperationException">A setting is missing or invalid; the message never includes the key.</exception>
    public static JwtOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);
        var errors = new List<string>();

        var issuer = section["Issuer"];
        if (string.IsNullOrWhiteSpace(issuer))
        {
            errors.Add($"'{SectionName}:Issuer' is not configured.");
        }

        var audience = section["Audience"];
        if (string.IsNullOrWhiteSpace(audience))
        {
            errors.Add($"'{SectionName}:Audience' is not configured.");
        }

        var signingKey = ReadSigningKey(section["SigningKey"], errors);
        var accessTokenMinutes = ReadPositiveInteger(section, "AccessTokenMinutes", errors);
        var refreshTokenDays = ReadPositiveInteger(section, "RefreshTokenDays", errors);

        if (errors.Count > 0)
        {
            throw new InvalidOperationException("Invalid JWT configuration: " + string.Join(" ", errors));
        }

        return new JwtOptions(
            issuer!,
            audience!,
            new SymmetricSecurityKey(signingKey),
            TimeSpan.FromMinutes(accessTokenMinutes),
            TimeSpan.FromDays(refreshTokenDays));
    }

    private static byte[] ReadSigningKey(string? value, List<string> errors)
    {
        const string howToSet =
            $"Set it with 'dotnet user-secrets' for local development or the '{SectionName}__SigningKey' " +
            "environment variable elsewhere, using at least 32 random bytes encoded as base64.";

        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"'{SectionName}:SigningKey' is not configured. {howToSet}");
            return [];
        }

        var buffer = new byte[value.Length];
        if (!Convert.TryFromBase64String(value, buffer, out var length))
        {
            errors.Add($"'{SectionName}:SigningKey' is not valid base64. {howToSet}");
            return [];
        }

        if (length < MinimumSigningKeyBytes)
        {
            errors.Add($"'{SectionName}:SigningKey' must decode to at least {MinimumSigningKeyBytes} bytes. {howToSet}");
            return [];
        }

        return buffer[..length];
    }

    private static int ReadPositiveInteger(IConfigurationSection section, string key, List<string> errors)
    {
        if (int.TryParse(section[key], out var value) && value > 0)
        {
            return value;
        }

        errors.Add($"'{SectionName}:{key}' must be a positive integer.");
        return 0;
    }
}
