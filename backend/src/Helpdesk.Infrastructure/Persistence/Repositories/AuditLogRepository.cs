using Helpdesk.Application.Abstractions;
using Helpdesk.Domain.Entities;

namespace Helpdesk.Infrastructure.Persistence.Repositories;

internal sealed class AuditLogRepository(HelpdeskDbContext context) : IAuditLogRepository
{
    public void Add(AuditLog entry) => context.AuditLogs.Add(entry);
}
