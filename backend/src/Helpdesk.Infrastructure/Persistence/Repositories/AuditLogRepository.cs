using Helpdesk.Application.Abstractions;
using Helpdesk.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.Infrastructure.Persistence.Repositories;

internal sealed class AuditLogRepository(HelpdeskDbContext context) : IAuditLogRepository
{
    public void Add(AuditLog entry) => context.AuditLogs.Add(entry);

    /// <summary>
    /// Served by the (ticket_id, changed_at) index. Not tracked: the history is only read, never changed.
    /// </summary>
    public async Task<IReadOnlyList<AuditLog>> ListByTicketAsync(Guid ticketId, CancellationToken cancellationToken) =>
        await context.AuditLogs
            .AsNoTracking()
            .Where(entry => entry.TicketId == ticketId)
            // Id breaks ties between entries recorded at the same instant, so the order is always the same.
            .OrderBy(entry => entry.ChangedAt)
            .ThenBy(entry => entry.Id)
            .ToListAsync(cancellationToken);
}
