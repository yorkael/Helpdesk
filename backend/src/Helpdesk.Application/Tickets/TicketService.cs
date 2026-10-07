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
    IValidator<ListTicketsRequest> listValidator,
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

    /// <param name="userId">The authenticated user; never taken from the query string.</param>
    /// <param name="role">The authenticated user's role; decides which tickets they may see.</param>
    public async Task<PagedResponse<TicketListItem>> ListAsync(
        ListTicketsRequest request,
        Guid userId,
        UserRole role,
        CancellationToken cancellationToken)
    {
        await listValidator.ValidateAndThrowAsync(request, cancellationToken);

        var query = new TicketListQuery(
            VisibilityFor(role),
            userId,
            request.Status is null ? null : Enum.Parse<TicketStatus>(request.Status),
            request.Priority is null ? null : Enum.Parse<TicketPriority>(request.Priority),
            request.AssignedToId,
            request.CategoryId,
            string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim(),
            request.Page ?? 1,
            request.PageSize ?? TicketLimits.DefaultPageSize);

        return await tickets.ListAsync(query, cancellationToken);
    }

    // An unknown role must never widen the scope, so it fails instead of falling back to All.
    private static TicketVisibility VisibilityFor(UserRole role) => role switch
    {
        UserRole.Admin => TicketVisibility.All,
        UserRole.Agent => TicketVisibility.AssignedToUserOrUnassigned,
        UserRole.Client => TicketVisibility.CreatedByUser,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown role.")
    };

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
