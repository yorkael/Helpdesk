using Helpdesk.Application.Abstractions;
using Helpdesk.Domain.Entities;

namespace Helpdesk.Tests.Application.Tickets;

internal sealed class InMemoryTicketRepository : ITicketRepository
{
    public List<Ticket> Tickets { get; } = [];

    public void Add(Ticket ticket) => Tickets.Add(ticket);
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
