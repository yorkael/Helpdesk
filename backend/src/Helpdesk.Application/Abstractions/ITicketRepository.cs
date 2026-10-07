using Helpdesk.Application.Tickets;
using Helpdesk.Domain.Entities;

namespace Helpdesk.Application.Abstractions;

public interface ITicketRepository
{
    void Add(Ticket ticket);

    Task<PagedResponse<TicketListItem>> ListAsync(TicketListQuery query, CancellationToken cancellationToken);

    /// <returns>The ticket, or null when it does not exist or is outside what the user may see.</returns>
    Task<Ticket?> GetVisibleAsync(
        Guid id,
        TicketVisibility visibility,
        Guid userId,
        CancellationToken cancellationToken);
}
