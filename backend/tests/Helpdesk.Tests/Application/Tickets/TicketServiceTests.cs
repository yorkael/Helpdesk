using FluentValidation;
using Helpdesk.Application.Common;
using Helpdesk.Application.Tickets;
using Helpdesk.Domain.Entities;
using Helpdesk.Domain.Enums;
using Helpdesk.Domain.Exceptions;
using Helpdesk.Tests.Application.Authentication;

namespace Helpdesk.Tests.Application.Tickets;

public class TicketServiceTests
{
    private static readonly Guid CategoryId = Guid.NewGuid();
    private static readonly Guid CreatorId = Guid.NewGuid();
    private static readonly Guid AdminId = Guid.NewGuid();

    private readonly InMemoryTicketRepository _tickets = new();
    private readonly InMemoryAuditLogRepository _auditLogs = new();
    private readonly InMemoryUserRepository _users = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 10, 7, 9, 30, 0, TimeSpan.Zero));
    private readonly TicketService _ticketService;

    public TicketServiceTests()
    {
        _ticketService = new TicketService(
            _tickets,
            _auditLogs,
            _unitOfWork,
            new CreateTicketRequestValidator(new InMemoryCategoryRepository(CategoryId)),
            new ListTicketsRequestValidator(),
            new AssignTicketRequestValidator(_users),
            new ChangeTicketStatusRequestValidator(),
            _time);
    }

    [Fact]
    public async Task Create_opens_an_unassigned_ticket_owned_by_the_creator()
    {
        var response = await _ticketService.CreateAsync(ValidRequest(), CreatorId, CancellationToken.None);

        var ticket = Assert.Single(_tickets.Tickets);
        Assert.Equal(TicketStatus.Open, ticket.Status);
        Assert.Equal(TicketPriority.High, ticket.Priority);
        Assert.Equal(CreatorId, ticket.CreatedById);
        Assert.Null(ticket.AssignedToId);
        Assert.Equal(_time.Now, ticket.CreatedAt);
        Assert.Equal(
            new TicketResponse(ticket.Id, "Printer offline", "It shows error 42.", "Open", "High", CategoryId, CreatorId, null, _time.Now),
            response);
    }

    [Fact]
    public async Task Create_records_the_creation_and_saves_it_with_the_ticket_in_one_save()
    {
        await _ticketService.CreateAsync(ValidRequest(), CreatorId, CancellationToken.None);

        var ticket = Assert.Single(_tickets.Tickets);
        var entry = Assert.Single(_auditLogs.Entries);
        Assert.Equal(ticket.Id, entry.TicketId);
        Assert.Equal(CreatorId, entry.UserId);
        Assert.Equal("Status", entry.Field);
        Assert.Null(entry.OldValue);
        Assert.Equal("Open", entry.NewValue);
        Assert.Equal(_time.Now, entry.ChangedAt);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Create_trims_title_and_description()
    {
        var response = await _ticketService.CreateAsync(
            ValidRequest() with { Title = "  Printer offline ", Description = " It shows error 42.  " },
            CreatorId,
            CancellationToken.None);

        Assert.Equal("Printer offline", response.Title);
        Assert.Equal("It shows error 42.", response.Description);
    }

    [Fact]
    public async Task Invalid_request_is_rejected_before_anything_is_stored()
    {
        await Assert.ThrowsAsync<ValidationException>(() => _ticketService.CreateAsync(
            ValidRequest() with { Priority = "1", CategoryId = Guid.NewGuid() },
            CreatorId,
            CancellationToken.None));

        Assert.Empty(_tickets.Tickets);
        Assert.Empty(_auditLogs.Entries);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Theory]
    [InlineData(UserRole.Client, TicketVisibility.CreatedByUser)]
    [InlineData(UserRole.Agent, TicketVisibility.AssignedToUserOrUnassigned)]
    [InlineData(UserRole.Admin, TicketVisibility.All)]
    public async Task List_scopes_the_tickets_by_the_callers_role(UserRole role, TicketVisibility expected)
    {
        var userId = Guid.NewGuid();

        await _ticketService.ListAsync(EmptyListRequest(), userId, role, CancellationToken.None);

        var query = _tickets.LastListQuery!;
        Assert.Equal(expected, query.Visibility);
        Assert.Equal(userId, query.UserId);
    }

    [Fact]
    public async Task List_rejects_an_unknown_role_instead_of_showing_every_ticket()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _ticketService.ListAsync(EmptyListRequest(), CreatorId, (UserRole)99, CancellationToken.None));

        Assert.Null(_tickets.LastListQuery);
    }

    [Fact]
    public async Task List_uses_the_first_page_and_default_size_when_they_are_missing()
    {
        await _ticketService.ListAsync(EmptyListRequest(), CreatorId, UserRole.Client, CancellationToken.None);

        var query = _tickets.LastListQuery!;
        Assert.Equal(1, query.Page);
        Assert.Equal(20, query.PageSize);
        Assert.Null(query.Status);
        Assert.Null(query.Priority);
        Assert.Null(query.AssignedToId);
        Assert.Null(query.CategoryId);
        Assert.Null(query.Search);
    }

    [Fact]
    public async Task List_passes_the_filters_on_with_enum_names_parsed()
    {
        var agentId = Guid.NewGuid();

        await _ticketService.ListAsync(
            new ListTicketsRequest(3, 50, "WaitingOnCustomer", "Urgent", agentId, CategoryId, "printer"),
            CreatorId,
            UserRole.Admin,
            CancellationToken.None);

        Assert.Equal(
            new TicketListQuery(
                TicketVisibility.All,
                CreatorId,
                TicketStatus.WaitingOnCustomer,
                TicketPriority.Urgent,
                agentId,
                CategoryId,
                "printer",
                3,
                50),
            _tickets.LastListQuery);
    }

    [Theory]
    [InlineData("  printer ", "printer")]
    [InlineData("100%", "100%")]
    [InlineData("", null)]
    [InlineData("   ", null)]
    public async Task List_trims_the_search_and_drops_it_when_blank(string search, string? expected)
    {
        await _ticketService.ListAsync(
            EmptyListRequest() with { Search = search },
            CreatorId,
            UserRole.Client,
            CancellationToken.None);

        Assert.Equal(expected, _tickets.LastListQuery!.Search);
    }

    [Fact]
    public async Task List_returns_the_repository_page_unchanged()
    {
        _tickets.ListResult = new PagedResponse<TicketListItem>([], 2, 10, 15);

        var response = await _ticketService.ListAsync(
            EmptyListRequest() with { Page = 2, PageSize = 10 },
            CreatorId,
            UserRole.Client,
            CancellationToken.None);

        Assert.Same(_tickets.ListResult, response);
    }

    [Fact]
    public async Task Invalid_list_request_never_reaches_the_repository()
    {
        await Assert.ThrowsAsync<ValidationException>(() => _ticketService.ListAsync(
            EmptyListRequest() with { Page = 0, Status = "open" },
            CreatorId,
            UserRole.Admin,
            CancellationToken.None));

        Assert.Null(_tickets.LastListQuery);
    }

    [Fact]
    public async Task List_neither_audits_nor_saves()
    {
        await _ticketService.ListAsync(EmptyListRequest(), CreatorId, UserRole.Admin, CancellationToken.None);

        Assert.Empty(_auditLogs.Entries);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Agent_takes_an_unassigned_ticket_and_the_change_is_audited_in_one_save()
    {
        var agent = SeedAgent();
        var ticket = SeedTicket();

        var response = await _ticketService.AssignAsync(
            ticket.Id, new AssignTicketRequest(agent.Id), agent.Id, UserRole.Agent, CancellationToken.None);

        Assert.Equal(agent.Id, ticket.AssignedToId);
        Assert.Equal(agent.Id, response.AssignedToId);
        var entry = Assert.Single(_auditLogs.Entries);
        Assert.Equal(ticket.Id, entry.TicketId);
        Assert.Equal(agent.Id, entry.UserId);
        Assert.Equal("AssignedToId", entry.Field);
        Assert.Null(entry.OldValue);
        Assert.Equal(agent.Id.ToString(), entry.NewValue);
        Assert.Equal(_time.Now, entry.ChangedAt);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Admin_reassigns_a_ticket_and_the_audit_keeps_both_assignees_and_the_admin_as_actor()
    {
        var first = SeedAgent("eva@example.com");
        var second = SeedAgent("mario@example.com");
        var ticket = SeedTicket(first.Id);

        var response = await _ticketService.AssignAsync(
            ticket.Id, new AssignTicketRequest(second.Id), AdminId, UserRole.Admin, CancellationToken.None);

        Assert.Equal(second.Id, response.AssignedToId);
        var entry = Assert.Single(_auditLogs.Entries);
        Assert.Equal(AdminId, entry.UserId);
        Assert.Equal(first.Id.ToString(), entry.OldValue);
        Assert.Equal(second.Id.ToString(), entry.NewValue);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Theory]
    [InlineData(UserRole.Agent, TicketVisibility.AssignedToUserOrUnassigned)]
    [InlineData(UserRole.Admin, TicketVisibility.All)]
    public async Task Assign_loads_the_ticket_within_the_callers_scope(UserRole role, TicketVisibility expected)
    {
        var agent = SeedAgent();
        var ticket = SeedTicket();
        var actorId = role == UserRole.Agent ? agent.Id : AdminId;

        await _ticketService.AssignAsync(ticket.Id, new AssignTicketRequest(agent.Id), actorId, role, CancellationToken.None);

        Assert.Equal((expected, actorId), _tickets.LastVisibleLookup);
    }

    [Fact]
    public async Task Agent_resending_their_own_id_on_their_ticket_changes_nothing()
    {
        var agent = SeedAgent();
        var ticket = SeedTicket(agent.Id);

        var response = await _ticketService.AssignAsync(
            ticket.Id, new AssignTicketRequest(agent.Id), agent.Id, UserRole.Agent, CancellationToken.None);

        Assert.Equal(agent.Id, response.AssignedToId);
        Assert.Empty(_auditLogs.Entries);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Agent_assigning_another_agent_is_forbidden_and_nothing_is_saved(bool ticketIsTheirs)
    {
        var agent = SeedAgent("eva@example.com");
        var otherAgent = SeedAgent("mario@example.com");
        var ticket = SeedTicket(ticketIsTheirs ? agent.Id : null);

        await Assert.ThrowsAsync<TicketActionForbiddenException>(() => _ticketService.AssignAsync(
            ticket.Id, new AssignTicketRequest(otherAgent.Id), agent.Id, UserRole.Agent, CancellationToken.None));

        Assert.Equal(ticketIsTheirs ? agent.Id : null, ticket.AssignedToId);
        AssertNothingSaved();
    }

    [Fact]
    public async Task Assigning_a_ticket_that_cannot_be_loaded_is_not_found()
    {
        var agent = SeedAgent();

        await Assert.ThrowsAsync<TicketNotFoundException>(() => _ticketService.AssignAsync(
            Guid.NewGuid(), new AssignTicketRequest(agent.Id), AdminId, UserRole.Admin, CancellationToken.None));

        AssertNothingSaved();
    }

    [Fact]
    public async Task Invalid_assignee_is_rejected_before_the_ticket_is_loaded()
    {
        var inactiveAgent = SeedAgent();
        TestUsers.Deactivate(inactiveAgent);
        var ticket = SeedTicket();

        await Assert.ThrowsAsync<ValidationException>(() => _ticketService.AssignAsync(
            ticket.Id, new AssignTicketRequest(inactiveAgent.Id), AdminId, UserRole.Admin, CancellationToken.None));

        Assert.Null(_tickets.LastVisibleLookup);
        AssertNothingSaved();
    }

    [Fact]
    public async Task Assigning_a_closed_ticket_breaks_a_domain_rule_and_saves_nothing()
    {
        var agent = SeedAgent();
        var ticket = SeedTicket();
        ticket.ChangeStatus(TicketStatus.Closed, AdminId, _time.Now);

        await Assert.ThrowsAsync<TicketRuleViolationException>(() => _ticketService.AssignAsync(
            ticket.Id, new AssignTicketRequest(agent.Id), AdminId, UserRole.Admin, CancellationToken.None));

        Assert.Null(ticket.AssignedToId);
        AssertNothingSaved();
    }

    [Fact]
    public async Task Unknown_ticket_is_reported_before_the_agent_role_rule()
    {
        var agent = SeedAgent("eva@example.com");
        var otherAgent = SeedAgent("mario@example.com");

        await Assert.ThrowsAsync<TicketNotFoundException>(() => _ticketService.AssignAsync(
            Guid.NewGuid(), new AssignTicketRequest(otherAgent.Id), agent.Id, UserRole.Agent, CancellationToken.None));
    }

    [Fact]
    public async Task Agent_role_rule_is_checked_before_the_domain_rule()
    {
        var agent = SeedAgent("eva@example.com");
        var otherAgent = SeedAgent("mario@example.com");
        var ticket = SeedTicket();
        ticket.ChangeStatus(TicketStatus.Closed, AdminId, _time.Now);

        await Assert.ThrowsAsync<TicketActionForbiddenException>(() => _ticketService.AssignAsync(
            ticket.Id, new AssignTicketRequest(otherAgent.Id), agent.Id, UserRole.Agent, CancellationToken.None));
    }

    [Fact]
    public async Task Concurrent_assignment_surfaces_as_a_concurrency_conflict()
    {
        var agent = SeedAgent();
        var ticket = SeedTicket();
        _unitOfWork.ExceptionOnNextSave = new ConcurrencyConflictException(new Exception());

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => _ticketService.AssignAsync(
            ticket.Id, new AssignTicketRequest(agent.Id), agent.Id, UserRole.Agent, CancellationToken.None));
    }

    [Fact]
    public async Task Agent_moves_their_ticket_to_in_progress_and_the_change_is_audited_in_one_save()
    {
        var agent = SeedAgent();
        var ticket = SeedTicket(agent.Id);

        var response = await _ticketService.ChangeStatusAsync(
            ticket.Id, new ChangeTicketStatusRequest("InProgress"), agent.Id, UserRole.Agent, CancellationToken.None);

        Assert.Equal("InProgress", response.Status);
        var entry = Assert.Single(_auditLogs.Entries);
        Assert.Equal(ticket.Id, entry.TicketId);
        Assert.Equal(agent.Id, entry.UserId);
        Assert.Equal("Status", entry.Field);
        Assert.Equal("Open", entry.OldValue);
        Assert.Equal("InProgress", entry.NewValue);
        Assert.Equal(_time.Now, entry.ChangedAt);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Admin_closes_an_unassigned_ticket()
    {
        var ticket = SeedTicket();

        var response = await _ticketService.ChangeStatusAsync(
            ticket.Id, new ChangeTicketStatusRequest("Closed"), AdminId, UserRole.Admin, CancellationToken.None);

        Assert.Equal("Closed", response.Status);
        Assert.Equal(AdminId, Assert.Single(_auditLogs.Entries).UserId);
    }

    [Theory]
    [InlineData(UserRole.Agent, TicketVisibility.AssignedToUserOrUnassigned)]
    [InlineData(UserRole.Admin, TicketVisibility.All)]
    public async Task Status_change_loads_the_ticket_within_the_callers_scope(UserRole role, TicketVisibility expected)
    {
        var ticket = SeedTicket();
        var actorId = Guid.NewGuid();

        await _ticketService.ChangeStatusAsync(
            ticket.Id, new ChangeTicketStatusRequest("Closed"), actorId, role, CancellationToken.None);

        Assert.Equal((expected, actorId), _tickets.LastVisibleLookup);
    }

    [Fact]
    public async Task Changing_to_the_current_status_changes_nothing()
    {
        var ticket = SeedTicket();

        var response = await _ticketService.ChangeStatusAsync(
            ticket.Id, new ChangeTicketStatusRequest("Open"), AdminId, UserRole.Admin, CancellationToken.None);

        Assert.Equal("Open", response.Status);
        AssertNothingSaved();
    }

    [Fact]
    public async Task Disallowed_transition_breaks_a_domain_rule_and_saves_nothing()
    {
        var agent = SeedAgent();
        var ticket = SeedTicket(agent.Id);

        await Assert.ThrowsAsync<TicketRuleViolationException>(() => _ticketService.ChangeStatusAsync(
            ticket.Id, new ChangeTicketStatusRequest("Resolved"), agent.Id, UserRole.Agent, CancellationToken.None));

        Assert.Equal(TicketStatus.Open, ticket.Status);
        AssertNothingSaved();
    }

    [Fact]
    public async Task Unassigned_ticket_cannot_be_moved_to_in_progress()
    {
        var ticket = SeedTicket();

        await Assert.ThrowsAsync<TicketRuleViolationException>(() => _ticketService.ChangeStatusAsync(
            ticket.Id, new ChangeTicketStatusRequest("InProgress"), AdminId, UserRole.Admin, CancellationToken.None));

        AssertNothingSaved();
    }

    [Theory]
    [InlineData("")]
    [InlineData("open")]
    [InlineData("1")]
    public async Task Invalid_status_is_rejected_before_the_ticket_is_loaded(string status)
    {
        var ticket = SeedTicket();

        await Assert.ThrowsAsync<ValidationException>(() => _ticketService.ChangeStatusAsync(
            ticket.Id, new ChangeTicketStatusRequest(status), AdminId, UserRole.Admin, CancellationToken.None));

        Assert.Null(_tickets.LastVisibleLookup);
        AssertNothingSaved();
    }

    [Fact]
    public async Task Changing_the_status_of_a_ticket_that_cannot_be_loaded_is_not_found()
    {
        await Assert.ThrowsAsync<TicketNotFoundException>(() => _ticketService.ChangeStatusAsync(
            Guid.NewGuid(), new ChangeTicketStatusRequest("Closed"), AdminId, UserRole.Admin, CancellationToken.None));

        AssertNothingSaved();
    }

    // The request is also invalid, so passing proves the role is checked before validation and lookup.
    [Fact]
    public async Task Client_cannot_assign_or_change_status_even_on_their_own_ticket()
    {
        var ticket = SeedTicket();

        await Assert.ThrowsAsync<TicketActionForbiddenException>(() => _ticketService.AssignAsync(
            ticket.Id, new AssignTicketRequest(Guid.Empty), CreatorId, UserRole.Client, CancellationToken.None));
        await Assert.ThrowsAsync<TicketActionForbiddenException>(() => _ticketService.ChangeStatusAsync(
            ticket.Id, new ChangeTicketStatusRequest("open"), CreatorId, UserRole.Client, CancellationToken.None));

        Assert.Null(_tickets.LastVisibleLookup);
        AssertNothingSaved();
    }

    private User SeedAgent(string email = "eva@example.com")
    {
        var agent = new User("Eva Agent", email, "hash", UserRole.Agent);
        _users.Add(agent);
        return agent;
    }

    // Assigned through the domain, so every stored ticket is one the application could have produced.
    private Ticket SeedTicket(Guid? assigneeId = null)
    {
        var ticket = new Ticket("Printer offline", "It shows error 42.", TicketPriority.High, CategoryId, CreatorId, _time.Now);
        if (assigneeId is { } id)
        {
            ticket.Assign(id, AdminId, _time.Now);
        }

        _tickets.Add(ticket);
        return ticket;
    }

    private void AssertNothingSaved()
    {
        Assert.Empty(_auditLogs.Entries);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    private static CreateTicketRequest ValidRequest() =>
        new("Printer offline", "It shows error 42.", CategoryId, "High");

    private static ListTicketsRequest EmptyListRequest() => new(null, null, null, null, null, null, null);
}
