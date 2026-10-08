using Helpdesk.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Helpdesk.Tests.Persistence;

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18").Build();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public async Task<HelpdeskDbContext> CreateEmptyDatabaseContextAsync() =>
        CreateContext(await CreateEmptyDatabaseAsync());

    public async Task<HelpdeskDbContext> CreateMigratedDatabaseContextAsync()
    {
        var context = await CreateEmptyDatabaseContextAsync();
        await context.Database.MigrateAsync();
        return context;
    }

    /// <returns>The connection string of a new, fully migrated database.</returns>
    public async Task<string> CreateMigratedDatabaseAsync()
    {
        await using var context = await CreateMigratedDatabaseContextAsync();
        return context.Database.GetConnectionString()!;
    }

    public static HelpdeskDbContext CreateContext(string connectionString) =>
        TestConfiguration.CreateInfrastructureServices(connectionString).GetRequiredService<HelpdeskDbContext>();

    private async Task<string> CreateEmptyDatabaseAsync()
    {
        var databaseName = $"test_{Guid.NewGuid():N}";

        await using (var connection = new NpgsqlConnection(_container.GetConnectionString()))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE {databaseName}", connection);
            await command.ExecuteNonQueryAsync();
        }

        // Every test gets its own database and so its own pool. Pooled, a test reuses its connections instead of
        // opening a new one per command (about 1,600 per run without pooling, each a new TCP connection and server
        // process, which under load sometimes timed out). Idle connections are closed within seconds, so the pools of
        // databases no test uses again never pile up until the server refuses new clients.
        return new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Database = databaseName,
            MaxPoolSize = 10,
            ConnectionIdleLifetime = 2,
            ConnectionPruningInterval = 1,
            // Safety net only: twice the default, for a slow open on a loaded machine.
            Timeout = 30
        }.ConnectionString;
    }
}

[CollectionDefinition(Name)]
public sealed class PostgreSqlCollection : ICollectionFixture<PostgreSqlFixture>
{
    public const string Name = "PostgreSQL";
}
