using Helpdesk.Application.Tickets;
using Helpdesk.Domain.Entities;

namespace Helpdesk.Application.Abstractions;

public interface ITicketRepository
{
    void Add(Ticket ticket);

    Task<PagedResponse<TicketListItem>> ListAsync(TicketListQuery query, CancellationToken cancellationToken);
}
