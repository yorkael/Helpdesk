using FluentValidation;
using Helpdesk.Application.Abstractions;
using Helpdesk.Domain.Entities;
using Helpdesk.Domain.Enums;

namespace Helpdesk.Application.Tickets;

public sealed class TicketService(
    ITicketRepository tickets,
    IAuditLogRepository auditLogs,
    IUnitOfWork unitOfWork,
    IValidator<CreateTicketRequest> createValidator,
    TimeProvider timeProvider)
{
    /// <param name="creatorId">The authenticated user; never taken from the request body.</param>
    public async Task<TicketResponse> CreateAsync(
        CreateTicketRequest request,
        Guid creatorId,
        CancellationToken cancellationToken)
    {
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var ticket = new Ticket(
            request.Title,
            request.Description,
            Enum.Parse<TicketPriority>(request.Priority),
            request.CategoryId,
            creatorId,
            timeProvider.GetUtcNow());

        tickets.Add(ticket);
        auditLogs.Add(AuditLog.TicketCreated(ticket));

        // One save, one transaction: the ticket never exists without its creation entry.
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToResponse(ticket);
    }

    private static TicketResponse ToResponse(Ticket ticket) => new(
        ticket.Id,
        ticket.Title,
        ticket.Description,
        ticket.Status.ToString(),
        ticket.Priority.ToString(),
        ticket.CategoryId,
        ticket.CreatedById,
        ticket.AssignedToId,
        ticket.CreatedAt);
}
