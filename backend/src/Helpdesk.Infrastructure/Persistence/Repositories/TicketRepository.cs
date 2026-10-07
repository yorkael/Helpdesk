using Helpdesk.Application.Abstractions;
using Helpdesk.Application.Tickets;
using Helpdesk.Domain.Entities;

namespace Helpdesk.Infrastructure.Persistence.Repositories;

internal sealed class TicketRepository(HelpdeskDbContext context) : ITicketRepository
{
    public void Add(Ticket ticket) => context.Tickets.Add(ticket);

    // Placeholder until stage 2 of HU-5 implements the query.
    public Task<PagedResponse<TicketListItem>> ListAsync(TicketListQuery query, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
