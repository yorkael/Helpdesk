using Helpdesk.Domain.Entities;

namespace Helpdesk.Application.Abstractions;

public interface ITicketRepository
{
    void Add(Ticket ticket);
}
