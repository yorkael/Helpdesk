using Helpdesk.Domain.Entities;

namespace Helpdesk.Application.Abstractions;

public interface IAuditLogRepository
{
    void Add(AuditLog entry);
}
