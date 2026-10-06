using System.Security.Cryptography;
using Helpdesk.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Helpdesk.Tests;

internal static class TestConfiguration
{
    public const string UnusedConnectionString = "Host=localhost;Database=unused";

    // A fresh key per test run, so no signing key is ever committed.
    public static readonly string SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    public static Dictionary<string, string?> CreateSettings(string connectionString) => new()
    {
        ["ConnectionStrings:DefaultConnection"] = connectionString,
        ["Jwt:Issuer"] = "helpdesk-tests",
        ["Jwt:Audience"] = "helpdesk-tests-client",
        ["Jwt:SigningKey"] = SigningKey,
        ["Jwt:AccessTokenMinutes"] = "15",
        ["Jwt:RefreshTokenDays"] = "7"
    };

    public static IConfiguration CreateConfiguration(IDictionary<string, string?> settings) =>
        new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

    /// <summary>Infrastructure services in a new scope, so scoped ones (DbContext, repositories) can be resolved.</summary>
    public static IServiceProvider CreateInfrastructureServices(string connectionString = UnusedConnectionString) =>
        new ServiceCollection()
            .AddInfrastructure(CreateConfiguration(CreateSettings(connectionString)))
            .BuildServiceProvider()
            .CreateScope()
            .ServiceProvider;
}
