using Helpdesk.Application.Abstractions;
using Helpdesk.Domain.Entities;

namespace Helpdesk.Infrastructure.Persistence.Repositories;

internal sealed class TicketRepository(HelpdeskDbContext context) : ITicketRepository
{
    public void Add(Ticket ticket) => context.Tickets.Add(ticket);
}
