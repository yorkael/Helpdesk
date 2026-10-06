using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Helpdesk.Tests.Api;

/// <summary>
/// Hosts the API in memory with its own settings, including a random signing key, so tests never read
/// the developer's user secrets (loaded only in Development) and also run in CI.
/// </summary>
public sealed class HelpdeskApiFactory(string connectionString, IReadOnlyDictionary<string, string?>? overrides = null)
    : WebApplicationFactory<Program>
{
    public const string EnvironmentName = "Testing";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(EnvironmentName);

        var settings = TestConfiguration.CreateSettings(connectionString);
        foreach (var (key, value) in overrides ?? new Dictionary<string, string?>())
        {
            settings[key] = value;
        }

        // UseSetting values reach Program as host settings with the highest precedence,
        // so they also win over environment variables such as ConnectionStrings__DefaultConnection.
        foreach (var (key, value) in settings)
        {
            builder.UseSetting(key, value);
        }
    }
}
