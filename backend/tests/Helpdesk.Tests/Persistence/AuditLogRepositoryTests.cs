using Helpdesk.Application.Abstractions;
using Helpdesk.Domain.Entities;
using Helpdesk.Domain.Enums;
using Helpdesk.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Helpdesk.Tests.Persistence;

[Collection(PostgreSqlCollection.Name)]
public class AuditLogRepositoryTests(PostgreSqlFixture fixture) : IAsyncLifetime
{
    private static readonly DateTimeOffset UtcNow = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    private string _connectionString = null!;
    private User _client = null!;
    private User _agent = null!;
    private Ticket _ticket = null!;

    public async Task InitializeAsync()
    {
        _connectionString = await fixture.CreateMigratedDatabaseAsync();
        _client = await SaveUserAsync("ana@example.com", UserRole.Client, "Ana Ruiz");
        _agent = await SaveUserAsync("luis@example.com", UserRole.Agent, "Luis Vega");
        _ticket = await SaveTicketAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Entries_are_ordered_oldest_first_whatever_the_insert_order()
    {
        var newest = await SaveEntryAsync(_ticket, "InProgress", "Resolved", UtcNow.AddHours(2));
        var oldest = await SaveEntryAsync(_ticket, null, "Open", UtcNow);
        var middle = await SaveEntryAsync(_ticket, "Open", "InProgress", UtcNow.AddHours(1));

        var entries = await ListAsync(_ticket.Id);

        Assert.Equal([oldest.Id, middle.Id, newest.Id], entries.Select(entry => entry.Id));
    }

    [Fact]
    public async Task Entries_recorded_at_the_same_instant_are_ordered_by_id()
    {
        var first = new AuditLog(_ticket.Id, _agent.Id, AuditFields.Status, "Open", "InProgress", UtcNow);
        var second = new AuditLog(_ticket.Id, _agent.Id, AuditFields.Assignee, null, _agent.Id.ToString(), UtcNow);
        // PostgreSQL orders uuids by their bytes, which is the ordinal order of their text form.
        var ascendingIds = new[] { first.Id, second.Id }.OrderBy(id => id.ToString(), StringComparer.Ordinal).ToArray();
        await SaveEntriesAsync(new[] { first, second }.OrderByDescending(entry => entry.Id.ToString(), StringComparer.Ordinal));

        var entries = await ListAsync(_ticket.Id);

        Assert.Equal(ascendingIds, entries.Select(entry => entry.Id));
    }

    [Fact]
    public async Task Only_the_requested_tickets_entries_are_returned()
    {
        var otherTicket = await SaveTicketAsync();
        var mine = await SaveEntryAsync(_ticket, null, "Open", UtcNow);
        await SaveEntryAsync(otherTicket, null, "Open", UtcNow);

        var entries = await ListAsync(_ticket.Id);

        Assert.Equal(mine.Id, Assert.Single(entries).Id);
    }

    [Fact]
    public async Task Ticket_without_entries_returns_an_empty_list()
    {
        Assert.Empty(await ListAsync(_ticket.Id));
    }

    [Fact]
    public async Task Entries_keep_every_stored_value_and_track_nothing()
    {
        var stored = await SaveEntryAsync(_ticket, "Open", "InProgress", UtcNow);
        var services = TestConfiguration.CreateInfrastructureServices(_connectionString);

        var entries = await services.GetRequiredService<IAuditLogRepository>()
            .ListByTicketAsync(_ticket.Id, CancellationToken.None);

        var entry = Assert.Single(entries);
        Assert.Equal(stored.Id, entry.Id);
        Assert.Equal(_ticket.Id, entry.TicketId);
        Assert.Equal(_agent.Id, entry.UserId);
        Assert.Equal(AuditFields.Status, entry.Field);
        Assert.Equal("Open", entry.OldValue);
        Assert.Equal("InProgress", entry.NewValue);
        Assert.Equal(UtcNow, entry.ChangedAt);
        Assert.Empty(services.GetRequiredService<HelpdeskDbContext>().ChangeTracker.Entries());
    }

    private async Task<IReadOnlyList<AuditLog>> ListAsync(Guid ticketId) =>
        await TestConfiguration.CreateInfrastructureServices(_connectionString)
            .GetRequiredService<IAuditLogRepository>()
            .ListByTicketAsync(ticketId, CancellationToken.None);

    private async Task<User> SaveUserAsync(string email, UserRole role, string name)
    {
        await using var context = PostgreSqlFixture.CreateContext(_connectionString);
        var user = new User(name, email, "hash", role);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private async Task<Ticket> SaveTicketAsync()
    {
        await using var context = PostgreSqlFixture.CreateContext(_connectionString);
        var categoryId = await context.Categories.Where(c => c.Name == "General").Select(c => c.Id).SingleAsync();
        var ticket = new Ticket("Printer offline", "It shows error 42.", TicketPriority.Medium, categoryId, _client.Id, UtcNow);
        context.Tickets.Add(ticket);
        await context.SaveChangesAsync();
        return ticket;
    }

    private async Task<AuditLog> SaveEntryAsync(Ticket ticket, string? oldValue, string newValue, DateTimeOffset changedAt)
    {
        var entry = new AuditLog(ticket.Id, _agent.Id, AuditFields.Status, oldValue, newValue, changedAt);
        await SaveEntriesAsync([entry]);
        return entry;
    }

    // One save per entry, so rows are inserted in the given order.
    private async Task SaveEntriesAsync(IEnumerable<AuditLog> entries)
    {
        foreach (var entry in entries)
        {
            await using var context = PostgreSqlFixture.CreateContext(_connectionString);
            context.AuditLogs.Add(entry);
            await context.SaveChangesAsync();
        }
    }
}
