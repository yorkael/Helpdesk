using Helpdesk.Application.Tickets;
using Helpdesk.Domain.Entities;
using Helpdesk.Domain.Enums;
using Helpdesk.Tests.Application.Authentication;

namespace Helpdesk.Tests.Application.Tickets;

public class TicketHistoryServiceTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 7, 9, 30, 0, TimeSpan.Zero);

    private readonly InMemoryTicketRepository _tickets = new();
    private readonly InMemoryAuditLogRepository _auditLogs = new();
    private readonly InMemoryUserRepository _users = new();
    private readonly TicketHistoryService _historyService;
    private readonly User _client;
    private readonly User _agent;
    private readonly User _otherAgent;
    private readonly User _admin;

    public TicketHistoryServiceTests()
    {
        _historyService = new TicketHistoryService(_tickets, _auditLogs, _users);

        _client = SeedUser("Ana Client", "ana@example.com", UserRole.Client);
        _agent = SeedUser("Eva Agent", "eva@example.com", UserRole.Agent);
        _otherAgent = SeedUser("Mario Agent", "mario@example.com", UserRole.Agent);
        _admin = SeedUser("Admin User", "admin@example.com", UserRole.Admin);
    }

    [Fact]
    public async Task Admin_gets_every_change_in_order_with_who_made_it_and_when()
    {
        var ticket = SeedTicket();
        var assigned = Record(ticket.Assign(_agent.Id, _admin.Id, CreatedAt.AddMinutes(5)));
        var started = Record(ticket.ChangeStatus(TicketStatus.InProgress, _agent.Id, CreatedAt.AddMinutes(10)));

        var history = await ListAsAdminAsync(ticket.Id);

        Assert.Equal(
            [
                new TicketHistoryEntryResponse(
                    _auditLogs.Entries[0].Id, "Status", null, "Open", null, null, _client.Id, "Ana Client", CreatedAt),
                new TicketHistoryEntryResponse(
                    assigned.Id, "AssignedToId", null, _agent.Id.ToString(), null, "Eva Agent",
                    _admin.Id, "Admin User", CreatedAt.AddMinutes(5)),
                new TicketHistoryEntryResponse(
                    started.Id, "Status", "Open", "InProgress", null, null,
                    _agent.Id, "Eva Agent", CreatedAt.AddMinutes(10))
            ],
            history);
    }

    [Theory]
    [InlineData(UserRole.Client)]
    [InlineData(UserRole.Agent)]
    public async Task Non_admin_is_forbidden_before_the_ticket_is_looked_up(UserRole role)
    {
        var ticket = SeedTicket();
        var caller = role == UserRole.Client ? _client : _agent;

        await Assert.ThrowsAsync<TicketActionForbiddenException>(() =>
            _historyService.ListAsync(ticket.Id, caller.Id, role, CancellationToken.None));

        Assert.Null(_tickets.LastVisibleLookup);
        Assert.Empty(_users.NameLookups);
    }

    [Fact]
    public async Task Unknown_ticket_is_not_found_for_an_admin()
    {
        await Assert.ThrowsAsync<TicketNotFoundException>(() => ListAsAdminAsync(Guid.NewGuid()));

        Assert.Empty(_users.NameLookups);
    }

    [Fact]
    public async Task Admin_looks_the_ticket_up_among_all_tickets()
    {
        var ticket = SeedTicket();

        await ListAsAdminAsync(ticket.Id);

        Assert.Equal((TicketVisibility.All, _admin.Id), _tickets.LastVisibleLookup);
    }

    [Fact]
    public async Task Reassignment_names_both_assignees_and_first_assignment_has_no_previous_one()
    {
        var ticket = SeedTicket();
        Record(ticket.Assign(_agent.Id, _admin.Id, CreatedAt.AddMinutes(5)));
        Record(ticket.Assign(_otherAgent.Id, _admin.Id, CreatedAt.AddMinutes(10)));

        var history = await ListAsAdminAsync(ticket.Id);

        var first = history[1];
        Assert.Null(first.OldValue);
        Assert.Null(first.OldValueName);
        Assert.Equal("Eva Agent", first.NewValueName);
        var second = history[2];
        Assert.Equal(_agent.Id.ToString(), second.OldValue);
        Assert.Equal("Eva Agent", second.OldValueName);
        Assert.Equal(_otherAgent.Id.ToString(), second.NewValue);
        Assert.Equal("Mario Agent", second.NewValueName);
    }

    [Fact]
    public async Task Assignee_that_matches_no_user_keeps_the_id_with_a_null_name()
    {
        var ticket = SeedTicket();
        var unknownId = Guid.NewGuid();
        Record(new AuditLog(ticket.Id, _admin.Id, AuditFields.Assignee, null, unknownId.ToString(), CreatedAt.AddMinutes(5)));

        var history = await ListAsAdminAsync(ticket.Id);

        var entry = history[1];
        Assert.Equal(unknownId.ToString(), entry.NewValue);
        Assert.Null(entry.NewValueName);
        Assert.Equal("Admin User", entry.ChangedByName);
    }

    [Fact]
    public async Task Status_entries_carry_no_value_names()
    {
        var ticket = SeedTicket();
        Record(ticket.Assign(_agent.Id, _admin.Id, CreatedAt.AddMinutes(5)));
        Record(ticket.ChangeStatus(TicketStatus.InProgress, _agent.Id, CreatedAt.AddMinutes(10)));

        var history = await ListAsAdminAsync(ticket.Id);

        var statusEntries = history.Where(entry => entry.Field == AuditFields.Status).ToList();
        Assert.Equal(2, statusEntries.Count);
        Assert.All(statusEntries, entry =>
        {
            Assert.Null(entry.OldValueName);
            Assert.Null(entry.NewValueName);
        });
    }

    [Fact]
    public async Task Names_come_from_one_lookup_of_the_distinct_actors_and_assignees()
    {
        var ticket = SeedTicket();
        Record(ticket.Assign(_agent.Id, _admin.Id, CreatedAt.AddMinutes(5)));
        Record(ticket.ChangeStatus(TicketStatus.InProgress, _agent.Id, CreatedAt.AddMinutes(10)));
        Record(ticket.Assign(_otherAgent.Id, _admin.Id, CreatedAt.AddMinutes(15)));

        await ListAsAdminAsync(ticket.Id);

        var lookup = Assert.Single(_users.NameLookups);
        Assert.Equal(
            new[] { _client.Id, _agent.Id, _otherAgent.Id, _admin.Id }.Order(),
            lookup.Order());
    }

    [Fact]
    public async Task Ticket_without_entries_returns_an_empty_list_without_a_names_lookup()
    {
        var ticket = new Ticket("Printer offline", "It shows error 42.", TicketPriority.High, Guid.NewGuid(), _client.Id, CreatedAt);
        _tickets.Add(ticket);

        var history = await ListAsAdminAsync(ticket.Id);

        Assert.Empty(history);
        Assert.Empty(_users.NameLookups);
    }

    [Fact]
    public async Task Author_that_matches_no_user_is_reported_as_a_bug()
    {
        var ticket = SeedTicket();
        Record(new AuditLog(ticket.Id, Guid.NewGuid(), AuditFields.Status, "Open", "Closed", CreatedAt.AddMinutes(5)));

        await Assert.ThrowsAsync<InvalidOperationException>(() => ListAsAdminAsync(ticket.Id));
    }

    private Task<IReadOnlyList<TicketHistoryEntryResponse>> ListAsAdminAsync(Guid ticketId) =>
        _historyService.ListAsync(ticketId, _admin.Id, UserRole.Admin, CancellationToken.None);

    private User SeedUser(string name, string email, UserRole role)
    {
        var user = new User(name, email, "hash", role);
        _users.Add(user);
        return user;
    }

    // Stored with its creation entry, as TicketService does, so the history starts like a real one.
    private Ticket SeedTicket()
    {
        var ticket = new Ticket("Printer offline", "It shows error 42.", TicketPriority.High, Guid.NewGuid(), _client.Id, CreatedAt);
        _tickets.Add(ticket);
        Record(AuditLog.TicketCreated(ticket));
        return ticket;
    }

    private AuditLog Record(AuditLog? entry)
    {
        Assert.NotNull(entry);
        _auditLogs.Add(entry);
        return entry;
    }
}
