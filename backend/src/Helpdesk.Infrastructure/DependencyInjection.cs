using Helpdesk.Application.Abstractions;
using Helpdesk.Infrastructure.Authentication;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Helpdesk.Infrastructure;

public static class DependencyInjection
{
    private const string ConnectionStringName = "DefaultConnection";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' is not configured. " +
                "Set it with 'dotnet user-secrets' for local development or the " +
                $"'ConnectionStrings__{ConnectionStringName}' environment variable elsewhere.");
        }

        var jwtOptions = JwtOptions.FromConfiguration(configuration);

        services.AddDbContext<HelpdeskDbContext>(options => options
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention());

        // No registration timeout: it only cancels a token, which Npgsql stops honoring once the server accepts the
        // connection, so a database that never answers would still hold the check. The deadline lives in the check
        // as Npgsql's connection Timeout, which covers the TCP connection, the startup handshake and authentication.
        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database");

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<ITicketRepository, TicketRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<ICommentRepository, CommentRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddSingleton(jwtOptions);
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
        services.AddSingleton<IRefreshTokenIssuer, RefreshTokenIssuer>();

        // Built here, at startup, so the dummy hash for unknown emails already exists before the first login.
        services.AddSingleton<IPasswordHasher>(new IdentityPasswordHasher());

        return services;
    }
}
