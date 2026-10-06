using System.Security.Cryptography;
using Helpdesk.Infrastructure.Authentication;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Tests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Helpdesk.Tests.Api;

// In the PostgreSQL collection so it never runs in parallel with other API tests while it sets environment variables.
[Collection(PostgreSqlCollection.Name)]
public class ApiStartupTests
{
    private const string OtherConnectionString = "Host=machine-specific;Database=not-the-test-database";

    [Fact]
    public async Task Api_does_not_start_without_a_signing_key()
    {
        await using var factory = new HelpdeskApiFactory(
            TestConfiguration.UnusedConnectionString,
            new Dictionary<string, string?> { ["Jwt:SigningKey"] = "" });

        var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains("'Jwt:SigningKey' is not configured", exception.Message);
    }

    [Fact]
    public async Task Factory_settings_win_over_machine_environment_variables()
    {
        var machineKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        Environment.SetEnvironmentVariable("Jwt__SigningKey", machineKey);
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", OtherConnectionString);

        try
        {
            await using var factory = new HelpdeskApiFactory(TestConfiguration.UnusedConnectionString);
            using var scope = factory.Services.CreateScope();

            var jwtOptions = scope.ServiceProvider.GetRequiredService<JwtOptions>();
            var context = scope.ServiceProvider.GetRequiredService<HelpdeskDbContext>();

            Assert.Equal(HelpdeskApiFactory.EnvironmentName, scope.ServiceProvider.GetRequiredService<IHostEnvironment>().EnvironmentName);
            Assert.Equal(Convert.FromBase64String(TestConfiguration.SigningKey), jwtOptions.SigningKey.Key);
            Assert.Equal(TestConfiguration.UnusedConnectionString, context.Database.GetConnectionString());
        }
        finally
        {
            Environment.SetEnvironmentVariable("Jwt__SigningKey", null);
            Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", null);
        }
    }
}
