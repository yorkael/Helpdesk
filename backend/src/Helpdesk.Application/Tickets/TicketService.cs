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
    IValidator<AssignTicketRequest> assignValidator,
    IValidator<ChangeTicketStatusRequest> statusValidator,
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

    /// <summary>An agent takes a ticket they can see for themselves; an admin assigns or reassigns any ticket.</summary>
    /// <param name="actorId">The authenticated user; recorded in the audit history, never taken from the body.</param>
    /// <param name="role">The authenticated user's role; decides which tickets they may load and change.</param>
    /// <exception cref="TicketActionForbiddenException">A client, or an agent assigning someone else.</exception>
    /// <exception cref="TicketNotFoundException">The ticket does not exist or is outside the user's scope.</exception>
    public async Task<TicketResponse> AssignAsync(
        Guid ticketId,
        AssignTicketRequest request,
        Guid actorId,
        UserRole role,
        CancellationToken cancellationToken)
    {
        EnsureStaff(role);
        await assignValidator.ValidateAndThrowAsync(request, cancellationToken);
        var ticket = await LoadVisibleAsync(ticketId, actorId, role, cancellationToken);

        // An agent only sees unassigned tickets and their own, so this also stops them reassigning their own.
        if (role == UserRole.Agent && request.AssigneeId != actorId)
        {
            throw new TicketActionForbiddenException("Agents can only assign tickets to themselves.");
        }

        await SaveChangeAsync(ticket.Assign(request.AssigneeId, actorId, timeProvider.GetUtcNow()), cancellationToken);
        return ToResponse(ticket);
    }

    /// <param name="actorId">The authenticated user; recorded in the audit history, never taken from the body.</param>
    /// <param name="role">The authenticated user's role; decides which tickets they may load and change.</param>
    /// <exception cref="TicketActionForbiddenException">The caller is a client.</exception>
    /// <exception cref="TicketNotFoundException">The ticket does not exist or is outside the user's scope.</exception>
    public async Task<TicketResponse> ChangeStatusAsync(
        Guid ticketId,
        ChangeTicketStatusRequest request,
        Guid actorId,
        UserRole role,
        CancellationToken cancellationToken)
    {
        EnsureStaff(role);
        await statusValidator.ValidateAndThrowAsync(request, cancellationToken);
        var ticket = await LoadVisibleAsync(ticketId, actorId, role, cancellationToken);

        var status = Enum.Parse<TicketStatus>(request.Status);
        await SaveChangeAsync(ticket.ChangeStatus(status, actorId, timeProvider.GetUtcNow()), cancellationToken);
        return ToResponse(ticket);
    }

    // The API's staff policy already stops clients; repeated here so the service is safe on its own,
    // since a client's scope (their own tickets) would otherwise let them change them.
    private static void EnsureStaff(UserRole role)
    {
        if (role is not (UserRole.Admin or UserRole.Agent))
        {
            throw new TicketActionForbiddenException("Only support staff can change tickets.");
        }
    }

    private async Task<Ticket> LoadVisibleAsync(Guid ticketId, Guid userId, UserRole role, CancellationToken cancellationToken) =>
        await tickets.GetVisibleAsync(ticketId, VisibilityFor(role), userId, cancellationToken)
        ?? throw new TicketNotFoundException();

    // No entry means nothing changed, so there is nothing to save. Otherwise the change and its entry go in
    // one save, one transaction; a concurrent change surfaces as ConcurrencyConflictException.
    private async Task SaveChangeAsync(AuditLog? entry, CancellationToken cancellationToken)
    {
        if (entry is null)
        {
            return;
        }

        auditLogs.Add(entry);
        await unitOfWork.SaveChangesAsync(cancellationToken);
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
