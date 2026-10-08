using Helpdesk.Domain.Entities;

namespace Helpdesk.Application.Abstractions;

public interface IAuditLogRepository
{
    void Add(AuditLog entry);

    /// <returns>The ticket's audit entries, oldest first; the id breaks ties. Not tracked.</returns>
    Task<IReadOnlyList<AuditLog>> ListByTicketAsync(Guid ticketId, CancellationToken cancellationToken);
}
