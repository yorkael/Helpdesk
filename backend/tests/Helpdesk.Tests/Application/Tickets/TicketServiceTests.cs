using FluentValidation;
using Helpdesk.Application.Tickets;
using Helpdesk.Domain.Enums;

namespace Helpdesk.Tests.Application.Tickets;

public class TicketServiceTests
{
    private static readonly Guid CategoryId = Guid.NewGuid();
    private static readonly Guid CreatorId = Guid.NewGuid();

    private readonly InMemoryTicketRepository _tickets = new();
    private readonly InMemoryAuditLogRepository _auditLogs = new();
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

    private static CreateTicketRequest ValidRequest() =>
        new("Printer offline", "It shows error 42.", CategoryId, "High");

    private static ListTicketsRequest EmptyListRequest() => new(null, null, null, null, null, null, null);
}
