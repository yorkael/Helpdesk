using Helpdesk.Application.Abstractions;
using Helpdesk.Application.Tickets;
using Helpdesk.Domain.Entities;

namespace Helpdesk.Tests.Application.Tickets;

internal sealed class InMemoryTicketRepository : ITicketRepository
{
    public List<Ticket> Tickets { get; } = [];

    /// <summary>The filters the service sent; applying them is the real repository's job.</summary>
    public TicketListQuery? LastListQuery { get; private set; }

    public PagedResponse<TicketListItem> ListResult { get; set; } = new([], 1, 20, 0);

    public void Add(Ticket ticket) => Tickets.Add(ticket);

    public Task<PagedResponse<TicketListItem>> ListAsync(TicketListQuery query, CancellationToken cancellationToken)
    {
        LastListQuery = query;
        return Task.FromResult(ListResult);
    }

    /// <summary>Returns any stored ticket with the id; scoping by visibility is covered against the database.</summary>
    public Task<Ticket?> GetVisibleAsync(
        Guid id,
        TicketVisibility visibility,
        Guid userId,
        CancellationToken cancellationToken) =>
        Task.FromResult(Tickets.SingleOrDefault(ticket => ticket.Id == id));
}

internal sealed class InMemoryAuditLogRepository : IAuditLogRepository
{
    public List<AuditLog> Entries { get; } = [];

    public void Add(AuditLog entry) => Entries.Add(entry);
}

internal sealed class InMemoryCategoryRepository(params Guid[] existingIds) : ICategoryRepository
{
    public int LookupCount { get; private set; }

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken)
    {
        LookupCount++;
        return Task.FromResult(existingIds.Contains(id));
    }
}
