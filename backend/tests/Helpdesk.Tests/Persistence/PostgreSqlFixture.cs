using Helpdesk.Infrastructure;
using Helpdesk.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Helpdesk.Tests.Persistence;

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18").Build();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public async Task<HelpdeskDbContext> CreateEmptyDatabaseContextAsync()
    {
        var databaseName = $"test_{Guid.NewGuid():N}";

        await using (var connection = new NpgsqlConnection(_container.GetConnectionString()))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE {databaseName}", connection);
            await command.ExecuteNonQueryAsync();
        }

        var connectionString = new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Database = databaseName
        }.ConnectionString;

        return CreateContext(connectionString);
    }

    public async Task<HelpdeskDbContext> CreateMigratedDatabaseContextAsync()
    {
        var context = await CreateEmptyDatabaseContextAsync();
        await context.Database.MigrateAsync();
        return context;
    }

    public static HelpdeskDbContext CreateContext(string connectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString
            })
            .Build();

        var services = new ServiceCollection()
            .AddInfrastructure(configuration)
            .BuildServiceProvider();

        return services.CreateScope().ServiceProvider.GetRequiredService<HelpdeskDbContext>();
    }
}

[CollectionDefinition(Name)]
public sealed class PostgreSqlCollection : ICollectionFixture<PostgreSqlFixture>
{
    public const string Name = "PostgreSQL";
}
