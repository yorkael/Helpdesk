using Helpdesk.Domain.Entities;
using Helpdesk.Domain.Enums;
using Helpdesk.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Helpdesk.Tests.Persistence;

[Collection(PostgreSqlCollection.Name)]
public class MigrationTests(PostgreSqlFixture fixture)
{
    private static readonly DateTimeOffset UtcNow = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Migrations_apply_to_an_empty_database()
    {
        await using var context = await fixture.CreateEmptyDatabaseContextAsync();
        Assert.Empty(await context.Database.GetAppliedMigrationsAsync());

        await context.Database.MigrateAsync();

        Assert.Equal(context.Database.GetMigrations(), await context.Database.GetAppliedMigrationsAsync());
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
    }

    [Fact]
    public async Task Ticket_with_comment_and_audit_entry_round_trips()
    {
        await using var context = await fixture.CreateMigratedDatabaseContextAsync();
        var seed = await SeedTicketAsync(context);
        context.Comments.Add(new Comment(seed.Ticket.Id, seed.Client.Id, "Still failing.", isInternal: false, UtcNow));
        context.AuditLogs.Add(new AuditLog(seed.Ticket.Id, seed.Client.Id, "Status", null, "Open", UtcNow));
        await context.SaveChangesAsync();

        await using var readContext = OpenNewContext(context);
        var ticket = await readContext.Tickets
            .Include(t => t.Category)
            .Include(t => t.CreatedBy)
            .Include(t => t.Comments)
            .SingleAsync(t => t.Id == seed.Ticket.Id);

        Assert.Equal("Printer offline", ticket.Title);
        Assert.Equal(TicketStatus.Open, ticket.Status);
        Assert.Equal(TicketPriority.High, ticket.Priority);
        Assert.Equal(UtcNow, ticket.CreatedAt);
        Assert.Equal("Hardware", ticket.Category.Name);
        Assert.Equal(seed.Client.Email, ticket.CreatedBy.Email);
        Assert.Single(ticket.Comments);
        Assert.Single(await readContext.AuditLogs.Where(a => a.TicketId == ticket.Id).ToListAsync());

        var storedStatus = await readContext.Database
            .SqlQuery<string>($"SELECT status AS \"Value\" FROM tickets")
            .SingleAsync();
        Assert.Equal("Open", storedStatus);
    }

    [Fact]
    public async Task Deleting_a_category_with_tickets_is_restricted()
    {
        await using var context = await fixture.CreateMigratedDatabaseContextAsync();
        var seed = await SeedTicketAsync(context);

        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            context.Categories.Where(c => c.Id == seed.Category.Id).ExecuteDeleteAsync());

        Assert.Equal(PostgresErrorCodes.RestrictViolation, exception.SqlState);
    }

    [Fact]
    public async Task Deleting_the_creator_of_a_ticket_is_restricted()
    {
        await using var context = await fixture.CreateMigratedDatabaseContextAsync();
        var seed = await SeedTicketAsync(context);

        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            context.Users.Where(u => u.Id == seed.Client.Id).ExecuteDeleteAsync());

        Assert.Equal(PostgresErrorCodes.RestrictViolation, exception.SqlState);
    }

    [Fact]
    public async Task Deleting_a_ticket_with_audit_history_is_restricted()
    {
        await using var context = await fixture.CreateMigratedDatabaseContextAsync();
        var seed = await SeedTicketAsync(context);
        context.AuditLogs.Add(new AuditLog(seed.Ticket.Id, seed.Client.Id, "Status", null, "Open", UtcNow));
        await context.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            context.Tickets.Where(t => t.Id == seed.Ticket.Id).ExecuteDeleteAsync());

        Assert.Equal(PostgresErrorCodes.RestrictViolation, exception.SqlState);
    }

    [Fact]
    public async Task Deleting_the_assigned_agent_returns_the_ticket_to_the_unassigned_queue()
    {
        await using var context = await fixture.CreateMigratedDatabaseContextAsync();
        var seed = await SeedTicketAsync(context);
        var agent = new User("Luis Vega", "luis@example.com", "hash", UserRole.Agent);
        context.Users.Add(agent);
        await context.SaveChangesAsync();
        await context.Database.ExecuteSqlAsync(
            $"UPDATE tickets SET assigned_to_id = {agent.Id} WHERE id = {seed.Ticket.Id}");

        await context.Users.Where(u => u.Id == agent.Id).ExecuteDeleteAsync();

        await using var readContext = OpenNewContext(context);
        var ticket = await readContext.Tickets.SingleAsync(t => t.Id == seed.Ticket.Id);
        Assert.Null(ticket.AssignedToId);
    }

    [Fact]
    public async Task Deleting_a_ticket_removes_its_comments()
    {
        await using var context = await fixture.CreateMigratedDatabaseContextAsync();
        var seed = await SeedTicketAsync(context);
        context.Comments.Add(new Comment(seed.Ticket.Id, seed.Client.Id, "Any update?", isInternal: false, UtcNow));
        await context.SaveChangesAsync();

        await context.Tickets.Where(t => t.Id == seed.Ticket.Id).ExecuteDeleteAsync();

        Assert.Equal(0, await context.Comments.CountAsync());
    }

    [Fact]
    public async Task Duplicate_email_violates_the_unique_index()
    {
        await using var context = await fixture.CreateMigratedDatabaseContextAsync();
        context.Users.Add(new User("Ana Ruiz", "ana@example.com", "hash", UserRole.Client));
        await context.SaveChangesAsync();

        context.Users.Add(new User("Ana Duplicate", "ANA@example.com", "hash", UserRole.Client));
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        var postgresException = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgresException.SqlState);
    }

    [Fact]
    public async Task Is_active_defaults_to_true_for_rows_inserted_outside_ef()
    {
        await using var context = await fixture.CreateMigratedDatabaseContextAsync();
        var id = Guid.CreateVersion7();

        await context.Database.ExecuteSqlAsync(
            $"INSERT INTO users (id, name, email, password_hash, role) VALUES ({id}, 'Seed Admin', 'admin@example.com', 'hash', 'Admin')");

        var user = await context.Users.SingleAsync(u => u.Id == id);
        Assert.True(user.IsActive);
    }

    [Fact]
    public async Task Refresh_token_round_trips_with_a_row_version()
    {
        await using var context = await fixture.CreateMigratedDatabaseContextAsync();
        var token = await SeedRefreshTokenAsync(context, "token-hash");

        await using var readContext = OpenNewContext(context);
        var stored = await readContext.RefreshTokens.SingleAsync(t => t.Id == token.Id);

        Assert.Equal(token.UserId, stored.UserId);
        Assert.Equal("token-hash", stored.TokenHash);
        Assert.Equal(UtcNow, stored.CreatedAt);
        Assert.Equal(UtcNow.AddDays(7), stored.ExpiresAt);
        Assert.Null(stored.RevokedAt);
        Assert.NotEqual(0u, stored.Version);
    }

    [Fact]
    public async Task Duplicate_refresh_token_hash_violates_the_unique_index()
    {
        await using var context = await fixture.CreateMigratedDatabaseContextAsync();
        var token = await SeedRefreshTokenAsync(context, "token-hash");

        context.RefreshTokens.Add(new RefreshToken(token.UserId, "token-hash", UtcNow, UtcNow.AddDays(7)));
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        var postgresException = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgresException.SqlState);
    }

    [Fact]
    public async Task Deleting_a_user_removes_their_refresh_tokens()
    {
        await using var context = await fixture.CreateMigratedDatabaseContextAsync();
        var token = await SeedRefreshTokenAsync(context, "token-hash");

        await context.Users.Where(u => u.Id == token.UserId).ExecuteDeleteAsync();

        Assert.Equal(0, await context.RefreshTokens.CountAsync());
    }

    [Fact]
    public async Task Concurrent_revocation_of_the_same_token_is_rejected()
    {
        await using var context = await fixture.CreateMigratedDatabaseContextAsync();
        var token = await SeedRefreshTokenAsync(context, "token-hash");

        await using var firstContext = OpenNewContext(context);
        await using var secondContext = OpenNewContext(context);
        var firstCopy = await firstContext.RefreshTokens.SingleAsync(t => t.Id == token.Id);
        var secondCopy = await secondContext.RefreshTokens.SingleAsync(t => t.Id == token.Id);

        firstCopy.Revoke(UtcNow.AddMinutes(1));
        await firstContext.SaveChangesAsync();

        secondCopy.Revoke(UtcNow.AddMinutes(1));
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => secondContext.SaveChangesAsync());
    }

    private static async Task<RefreshToken> SeedRefreshTokenAsync(HelpdeskDbContext context, string tokenHash)
    {
        var user = new User("Ana Ruiz", "ana@example.com", "hash", UserRole.Client);
        var token = new RefreshToken(user.Id, tokenHash, UtcNow, UtcNow.AddDays(7));

        context.AddRange(user, token);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        return token;
    }

    private static async Task<(Category Category, User Client, Ticket Ticket)> SeedTicketAsync(HelpdeskDbContext context)
    {
        var category = new Category("Hardware");
        var client = new User("Ana Ruiz", "ana@example.com", "hash", UserRole.Client);
        var ticket = new Ticket("Printer offline", "It shows error 42.", TicketPriority.High, category.Id, client.Id, UtcNow);

        context.AddRange(category, client, ticket);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        return (category, client, ticket);
    }

    private static HelpdeskDbContext OpenNewContext(HelpdeskDbContext context) =>
        PostgreSqlFixture.CreateContext(context.Database.GetConnectionString()!);
}
