using Helpdesk.Application.Abstractions;
using Helpdesk.Application.Authentication;
using Helpdesk.Application.Common;
using Helpdesk.Domain.Entities;
using Helpdesk.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace Helpdesk.Tests.Persistence;

[Collection(PostgreSqlCollection.Name)]
public class RepositoryTests(PostgreSqlFixture fixture)
{
    private static readonly DateTimeOffset UtcNow = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task User_is_found_by_normalized_email_and_by_id()
    {
        var connectionString = await fixture.CreateMigratedDatabaseAsync();
        var user = await SaveUserAsync(connectionString, "ana@example.com");

        var users = Services(connectionString).GetRequiredService<IUserRepository>();

        Assert.Equal(user.Id, (await users.GetByEmailAsync("ana@example.com", CancellationToken.None))?.Id);
        Assert.Equal(user.Id, (await users.GetByIdAsync(user.Id, CancellationToken.None))?.Id);
        Assert.True(await users.EmailExistsAsync("ana@example.com", CancellationToken.None));
        Assert.False(await users.EmailExistsAsync("nobody@example.com", CancellationToken.None));
    }

    [Fact]
    public async Task Saving_a_duplicate_email_reports_it_as_already_registered()
    {
        var connectionString = await fixture.CreateMigratedDatabaseAsync();
        await SaveUserAsync(connectionString, "ana@example.com");

        var services = Services(connectionString);
        services.GetRequiredService<IUserRepository>()
            .Add(new User("Ana Again", "ana@example.com", "hash", UserRole.Client));

        await Assert.ThrowsAsync<EmailAlreadyRegisteredException>(() =>
            services.GetRequiredService<IUnitOfWork>().SaveChangesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Refresh_token_is_found_by_hash()
    {
        var connectionString = await fixture.CreateMigratedDatabaseAsync();
        var user = await SaveUserAsync(connectionString, "ana@example.com");
        var token = await SaveRefreshTokenAsync(connectionString, user.Id, "hash-1", UtcNow.AddDays(7));

        var stored = await Services(connectionString).GetRequiredService<IRefreshTokenRepository>()
            .GetByHashAsync("hash-1", CancellationToken.None);

        Assert.Equal(token.Id, stored?.Id);
    }

    [Fact]
    public async Task Active_tokens_exclude_revoked_expired_and_other_users_tokens()
    {
        var connectionString = await fixture.CreateMigratedDatabaseAsync();
        var ana = await SaveUserAsync(connectionString, "ana@example.com");
        var luis = await SaveUserAsync(connectionString, "luis@example.com");
        var active = await SaveRefreshTokenAsync(connectionString, ana.Id, "active", UtcNow.AddDays(7));
        await SaveRefreshTokenAsync(connectionString, ana.Id, "expired", UtcNow.AddMinutes(-1), UtcNow.AddDays(-7));
        await SaveRefreshTokenAsync(connectionString, ana.Id, "revoked", UtcNow.AddDays(7), revoke: true);
        await SaveRefreshTokenAsync(connectionString, luis.Id, "other-user", UtcNow.AddDays(7));

        var tokens = await Services(connectionString).GetRequiredService<IRefreshTokenRepository>()
            .GetActiveByUserAsync(ana.Id, UtcNow, CancellationToken.None);

        Assert.Equal(active.Id, Assert.Single(tokens).Id);
    }

    [Fact]
    public async Task Concurrent_change_is_reported_as_a_concurrency_conflict()
    {
        var connectionString = await fixture.CreateMigratedDatabaseAsync();
        var user = await SaveUserAsync(connectionString, "ana@example.com");
        await SaveRefreshTokenAsync(connectionString, user.Id, "hash-1", UtcNow.AddDays(7));

        var first = Services(connectionString);
        var second = Services(connectionString);
        var firstCopy = await first.GetRequiredService<IRefreshTokenRepository>().GetByHashAsync("hash-1", CancellationToken.None);
        var secondCopy = await second.GetRequiredService<IRefreshTokenRepository>().GetByHashAsync("hash-1", CancellationToken.None);

        firstCopy!.Revoke(UtcNow);
        await first.GetRequiredService<IUnitOfWork>().SaveChangesAsync(CancellationToken.None);

        secondCopy!.Revoke(UtcNow);
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            second.GetRequiredService<IUnitOfWork>().SaveChangesAsync(CancellationToken.None));
    }

    private static IServiceProvider Services(string connectionString) =>
        TestConfiguration.CreateInfrastructureServices(connectionString);

    private static async Task<User> SaveUserAsync(string connectionString, string email)
    {
        var services = Services(connectionString);
        var user = new User("Test User", email, "hash", UserRole.Client);
        services.GetRequiredService<IUserRepository>().Add(user);
        await services.GetRequiredService<IUnitOfWork>().SaveChangesAsync(CancellationToken.None);
        return user;
    }

    private static async Task<RefreshToken> SaveRefreshTokenAsync(
        string connectionString,
        Guid userId,
        string tokenHash,
        DateTimeOffset expiresAt,
        DateTimeOffset? createdAt = null,
        bool revoke = false)
    {
        var services = Services(connectionString);
        var token = new RefreshToken(userId, tokenHash, createdAt ?? UtcNow.AddDays(-1), expiresAt);
        if (revoke)
        {
            token.Revoke(UtcNow.AddHours(-1));
        }

        services.GetRequiredService<IRefreshTokenRepository>().Add(token);
        await services.GetRequiredService<IUnitOfWork>().SaveChangesAsync(CancellationToken.None);
        return token;
    }
}
