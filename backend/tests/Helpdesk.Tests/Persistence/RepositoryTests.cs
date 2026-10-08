using Helpdesk.Application.Abstractions;
using Helpdesk.Application.Authentication;
using Helpdesk.Application.Common;
using Helpdesk.Application.Tickets;
using Helpdesk.Domain.Entities;
using Helpdesk.Domain.Enums;
using Microsoft.EntityFrameworkCore;
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
    public async Task User_names_are_found_by_id_and_unknown_ids_are_left_out()
    {
        var connectionString = await fixture.CreateMigratedDatabaseAsync();
        var ana = await SaveUserAsync(connectionString, "ana@example.com", name: "Ana Ruiz");
        var luis = await SaveUserAsync(connectionString, "luis@example.com", UserRole.Agent, "Luis Vega");
        await SaveUserAsync(connectionString, "eva@example.com", name: "Eva Not Asked");

        var names = await Services(connectionString).GetRequiredService<IUserRepository>()
            .GetNamesAsync([ana.Id, luis.Id, Guid.NewGuid()], CancellationToken.None);

        Assert.Equal(
            new Dictionary<Guid, string> { [ana.Id] = "Ana Ruiz", [luis.Id] = "Luis Vega" },
            names);
    }

    [Fact]
    public async Task No_ids_give_no_names()
    {
        var connectionString = await fixture.CreateMigratedDatabaseAsync();
        await SaveUserAsync(connectionString, "ana@example.com");

        var names = await Services(connectionString).GetRequiredService<IUserRepository>()
            .GetNamesAsync([], CancellationToken.None);

        Assert.Empty(names);
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

    [Fact]
    public async Task Category_exists_only_for_a_stored_id()
    {
        var connectionString = await fixture.CreateMigratedDatabaseAsync();
        Guid seededCategoryId;
        await using (var context = PostgreSqlFixture.CreateContext(connectionString))
        {
            seededCategoryId = await context.Categories.Select(c => c.Id).FirstAsync();
        }

        var categories = Services(connectionString).GetRequiredService<ICategoryRepository>();

        Assert.True(await categories.ExistsAsync(seededCategoryId, CancellationToken.None));
        Assert.False(await categories.ExistsAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task Ticket_and_its_audit_entry_are_stored_by_one_save()
    {
        var connectionString = await fixture.CreateMigratedDatabaseAsync();
        var creator = await SaveUserAsync(connectionString, "ana@example.com");
        var services = Services(connectionString);
        var ticket = await NewTicketAsync(connectionString, creator.Id);

        services.GetRequiredService<ITicketRepository>().Add(ticket);
        services.GetRequiredService<IAuditLogRepository>().Add(AuditLog.TicketCreated(ticket));
        await services.GetRequiredService<IUnitOfWork>().SaveChangesAsync(CancellationToken.None);

        await using var context = PostgreSqlFixture.CreateContext(connectionString);
        Assert.Equal(ticket.Id, (await context.Tickets.SingleAsync()).Id);
        Assert.Equal(ticket.Id, (await context.AuditLogs.SingleAsync()).TicketId);
    }

    [Fact]
    public async Task Failed_audit_insert_rolls_back_the_ticket()
    {
        var connectionString = await fixture.CreateMigratedDatabaseAsync();
        var creator = await SaveUserAsync(connectionString, "ana@example.com");
        var services = Services(connectionString);
        var ticket = await NewTicketAsync(connectionString, creator.Id);

        services.GetRequiredService<ITicketRepository>().Add(ticket);
        // An unknown user breaks the audit entry's foreign key, so the save fails after the ticket insert.
        services.GetRequiredService<IAuditLogRepository>()
            .Add(new AuditLog(ticket.Id, Guid.NewGuid(), AuditFields.Status, null, "Open", UtcNow));

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            services.GetRequiredService<IUnitOfWork>().SaveChangesAsync(CancellationToken.None));

        await using var context = PostgreSqlFixture.CreateContext(connectionString);
        Assert.Equal(0, await context.Tickets.CountAsync());
        Assert.Equal(0, await context.AuditLogs.CountAsync());
    }

    [Theory]
    [InlineData(TicketVisibility.All, new[] { "client's", "agent's", "other agent's", "unassigned" })]
    [InlineData(TicketVisibility.CreatedByUser, new[] { "client's" })]
    [InlineData(TicketVisibility.AssignedToUserOrUnassigned, new[] { "agent's", "unassigned" })]
    public async Task Ticket_is_loaded_only_when_it_is_visible_to_the_user(TicketVisibility visibility, string[] expected)
    {
        var connectionString = await fixture.CreateMigratedDatabaseAsync();
        var client = await SaveUserAsync(connectionString, "ana@example.com");
        var otherClient = await SaveUserAsync(connectionString, "luis@example.com");
        var agent = await SaveUserAsync(connectionString, "eva@example.com", UserRole.Agent);
        var otherAgent = await SaveUserAsync(connectionString, "mario@example.com", UserRole.Agent);
        // The client looks under CreatedByUser; the agent under the other two (All ignores the user).
        var userId = visibility == TicketVisibility.CreatedByUser ? client.Id : agent.Id;
        var tickets = new Dictionary<string, Ticket>
        {
            ["client's"] = await SaveTicketAsync(connectionString, client.Id, otherAgent.Id),
            ["agent's"] = await SaveTicketAsync(connectionString, otherClient.Id, agent.Id),
            ["other agent's"] = await SaveTicketAsync(connectionString, otherClient.Id, otherAgent.Id),
            ["unassigned"] = await SaveTicketAsync(connectionString, otherClient.Id)
        };

        var repository = Services(connectionString).GetRequiredService<ITicketRepository>();
        var visible = new List<string>();
        foreach (var (name, ticket) in tickets)
        {
            if (await repository.GetVisibleAsync(ticket.Id, visibility, userId, CancellationToken.None) is not null)
            {
                visible.Add(name);
            }
        }

        Assert.Equal(expected, visible);
    }

    [Fact]
    public async Task Unknown_ticket_id_is_not_found()
    {
        var connectionString = await fixture.CreateMigratedDatabaseAsync();

        var ticket = await Services(connectionString).GetRequiredService<ITicketRepository>()
            .GetVisibleAsync(Guid.NewGuid(), TicketVisibility.All, Guid.NewGuid(), CancellationToken.None);

        Assert.Null(ticket);
    }

    [Fact]
    public async Task Loaded_ticket_change_and_its_audit_entry_are_stored_by_one_save()
    {
        var connectionString = await fixture.CreateMigratedDatabaseAsync();
        var client = await SaveUserAsync(connectionString, "ana@example.com");
        var agent = await SaveUserAsync(connectionString, "eva@example.com", UserRole.Agent);
        var saved = await SaveTicketAsync(connectionString, client.Id);
        var services = Services(connectionString);

        var ticket = await services.GetRequiredService<ITicketRepository>()
            .GetVisibleAsync(saved.Id, TicketVisibility.AssignedToUserOrUnassigned, agent.Id, CancellationToken.None);
        services.GetRequiredService<IAuditLogRepository>().Add(ticket!.Assign(agent.Id, agent.Id, UtcNow)!);
        await services.GetRequiredService<IUnitOfWork>().SaveChangesAsync(CancellationToken.None);

        await using var context = PostgreSqlFixture.CreateContext(connectionString);
        Assert.Equal(agent.Id, (await context.Tickets.SingleAsync()).AssignedToId);
        var entry = await context.AuditLogs.SingleAsync();
        Assert.Equal(AuditFields.Assignee, entry.Field);
        Assert.Null(entry.OldValue);
        Assert.Equal(agent.Id.ToString(), entry.NewValue);
    }

    [Fact]
    public async Task Two_agents_taking_the_same_ticket_leave_it_with_the_first_and_one_audit_entry()
    {
        var connectionString = await fixture.CreateMigratedDatabaseAsync();
        var client = await SaveUserAsync(connectionString, "ana@example.com");
        var first = await SaveUserAsync(connectionString, "eva@example.com", UserRole.Agent);
        var second = await SaveUserAsync(connectionString, "mario@example.com", UserRole.Agent);
        var saved = await SaveTicketAsync(connectionString, client.Id);
        var firstServices = Services(connectionString);
        var secondServices = Services(connectionString);

        // Both read the unassigned ticket before either saves.
        var firstCopy = await LoadForAgentAsync(firstServices, saved.Id, first.Id);
        var secondCopy = await LoadForAgentAsync(secondServices, saved.Id, second.Id);

        firstServices.GetRequiredService<IAuditLogRepository>().Add(firstCopy.Assign(first.Id, first.Id, UtcNow)!);
        await firstServices.GetRequiredService<IUnitOfWork>().SaveChangesAsync(CancellationToken.None);

        secondServices.GetRequiredService<IAuditLogRepository>().Add(secondCopy.Assign(second.Id, second.Id, UtcNow)!);
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            secondServices.GetRequiredService<IUnitOfWork>().SaveChangesAsync(CancellationToken.None));

        await using var context = PostgreSqlFixture.CreateContext(connectionString);
        Assert.Equal(first.Id, (await context.Tickets.SingleAsync()).AssignedToId);
        Assert.Equal(first.Id.ToString(), (await context.AuditLogs.SingleAsync()).NewValue);
    }

    [Fact]
    public async Task Failed_audit_insert_rolls_back_the_status_change()
    {
        var connectionString = await fixture.CreateMigratedDatabaseAsync();
        var client = await SaveUserAsync(connectionString, "ana@example.com");
        var agent = await SaveUserAsync(connectionString, "eva@example.com", UserRole.Agent);
        var saved = await SaveTicketAsync(connectionString, client.Id, agent.Id);
        var services = Services(connectionString);

        var ticket = await LoadForAgentAsync(services, saved.Id, agent.Id);
        ticket.ChangeStatus(TicketStatus.InProgress, agent.Id, UtcNow);
        // An unknown user breaks the audit entry's foreign key, so the save fails after the ticket update.
        services.GetRequiredService<IAuditLogRepository>()
            .Add(new AuditLog(ticket.Id, Guid.NewGuid(), AuditFields.Status, "Open", "InProgress", UtcNow));

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            services.GetRequiredService<IUnitOfWork>().SaveChangesAsync(CancellationToken.None));

        await using var context = PostgreSqlFixture.CreateContext(connectionString);
        Assert.Equal(TicketStatus.Open, (await context.Tickets.SingleAsync()).Status);
        Assert.Equal(0, await context.AuditLogs.CountAsync());
    }

    private static async Task<Ticket> LoadForAgentAsync(IServiceProvider services, Guid ticketId, Guid agentId) =>
        (await services.GetRequiredService<ITicketRepository>().GetVisibleAsync(
            ticketId, TicketVisibility.AssignedToUserOrUnassigned, agentId, CancellationToken.None))!;

    // Assigned through the domain, so the stored row is one the application could have produced.
    private static async Task<Ticket> SaveTicketAsync(string connectionString, Guid creatorId, Guid? assigneeId = null)
    {
        var ticket = await NewTicketAsync(connectionString, creatorId);
        if (assigneeId is { } id)
        {
            ticket.Assign(id, id, UtcNow);
        }

        var services = Services(connectionString);
        services.GetRequiredService<ITicketRepository>().Add(ticket);
        await services.GetRequiredService<IUnitOfWork>().SaveChangesAsync(CancellationToken.None);
        return ticket;
    }

    private static async Task<Ticket> NewTicketAsync(string connectionString, Guid creatorId)
    {
        await using var context = PostgreSqlFixture.CreateContext(connectionString);
        var categoryId = await context.Categories.Select(c => c.Id).FirstAsync();
        return new Ticket("Printer offline", "It shows error 42.", TicketPriority.High, categoryId, creatorId, UtcNow);
    }

    private static IServiceProvider Services(string connectionString) =>
        TestConfiguration.CreateInfrastructureServices(connectionString);

    private static async Task<User> SaveUserAsync(
        string connectionString,
        string email,
        UserRole role = UserRole.Client,
        string name = "Test User")
    {
        var services = Services(connectionString);
        var user = new User(name, email, "hash", role);
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
