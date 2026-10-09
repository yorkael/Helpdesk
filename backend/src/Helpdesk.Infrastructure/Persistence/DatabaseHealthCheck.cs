using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace Helpdesk.Infrastructure.Persistence;

/// <summary>
/// Reports whether PostgreSQL accepts connections. A failure surfaces as an exception, which the health check
/// service logs and turns into Unhealthy; the endpoint's response carries only the overall status.
/// </summary>
internal sealed class DatabaseHealthCheck(HelpdeskDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var connectionString = new NpgsqlConnectionStringBuilder(dbContext.Database.GetConnectionString())
        {
            // Seconds, for the whole open: TCP connection, startup handshake and authentication.
            Timeout = 3,
            // A pooled connection could be handed out without reaching the server, so every check opens a new one.
            Pooling = false
        }.ConnectionString;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        return HealthCheckResult.Healthy();
    }
}
