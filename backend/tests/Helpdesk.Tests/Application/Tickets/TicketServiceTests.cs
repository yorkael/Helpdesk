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

    private static CreateTicketRequest ValidRequest() =>
        new("Printer offline", "It shows error 42.", CategoryId, "High");
}
