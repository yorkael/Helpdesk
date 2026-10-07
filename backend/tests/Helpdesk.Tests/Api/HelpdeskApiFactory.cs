using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Helpdesk.Tests.Api;

/// <summary>
/// Hosts the API in memory with its own settings, including a random signing key, so tests never read
/// the developer's user secrets (loaded only in Development) and also run in CI.
/// </summary>
/// <param name="configureServices">Runs after the API registers its services, so a test can replace or extend them.</param>
public sealed class HelpdeskApiFactory(
    string connectionString,
    IReadOnlyDictionary<string, string?>? overrides = null,
    Action<IServiceCollection>? configureServices = null)
    : WebApplicationFactory<Program>
{
    public const string EnvironmentName = "Testing";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(EnvironmentName);

        // Development validates the container on startup; Testing does not by default, so a missing
        // registration would pass the tests and only fail under dotnet run.
        builder.UseDefaultServiceProvider(options =>
        {
            options.ValidateOnBuild = true;
            options.ValidateScopes = true;
        });

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

        if (configureServices is not null)
        {
            builder.ConfigureTestServices(configureServices);
        }
    }
}
