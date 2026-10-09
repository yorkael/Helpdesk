using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Helpdesk.Tests.Persistence;

namespace Helpdesk.Tests.Api;

/// <summary>
/// Covers the health endpoint used by container and platform probes: anonymous, 200 when PostgreSQL accepts
/// connections and 503 with no details when it does not.
/// </summary>
[Collection(PostgreSqlCollection.Name)]
public class HealthEndpointTests(PostgreSqlFixture fixture)
{
    [Fact]
    public async Task Reachable_database_returns_200_without_a_token()
    {
        await using var factory = new HelpdeskApiFactory(await fixture.CreateMigratedDatabaseAsync());
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Unreachable_database_returns_503_without_details()
    {
        // Port 1 on loopback refuses connections. Not TestConfiguration.UnusedConnectionString: it points to
        // localhost:5432, where a developer's local database may be running.
        await using var factory = new HelpdeskApiFactory("Host=127.0.0.1;Port=1;Database=unreachable");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Unhealthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Unresponsive_database_fails_within_the_check_timeout()
    {
        // The operating system completes the TCP handshake for a started listener, but nothing ever reads or
        // answers, like a database that hangs. Npgsql would wait the 60 seconds of the connection string.
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            await using var factory = new HelpdeskApiFactory($"Host=127.0.0.1;Port={port};Database=unresponsive;Timeout=60");
            using var client = factory.CreateClient();
            var stopwatch = Stopwatch.StartNew();

            var response = await client.GetAsync("/health");

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10), $"The check took {stopwatch.Elapsed}.");
        }
        finally
        {
            listener.Stop();
        }
    }
}
