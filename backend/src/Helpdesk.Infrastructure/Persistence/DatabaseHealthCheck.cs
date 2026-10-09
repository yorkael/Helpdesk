using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Helpdesk.Infrastructure.Persistence;

/// <summary>
/// Reports whether PostgreSQL accepts connections. The result carries no exception or description, so nothing
/// about the database reaches an anonymous caller.
/// </summary>
internal sealed class DatabaseHealthCheck(HelpdeskDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        await dbContext.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy();
}
