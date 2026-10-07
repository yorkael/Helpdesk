using FluentValidation;
using Helpdesk.Application.Abstractions;
using Helpdesk.Domain.Entities;
using Helpdesk.Domain.Enums;

namespace Helpdesk.Application.Tickets;

/// <summary>
/// Comments on a ticket the user may see. Checks run in this order: request (400), ticket within the
/// user's scope (404), role (403), domain rule (409).
/// </summary>
public sealed class TicketCommentService(
    ITicketRepository tickets,
    ICommentRepository comments,
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IValidator<CreateCommentRequest> createValidator,
    TimeProvider timeProvider)
{
    /// <param name="authorId">The authenticated user; never taken from the request body.</param>
    /// <param name="role">The authenticated user's role; decides which tickets they may comment on.</param>
    /// <exception cref="TicketNotFoundException">The ticket does not exist or is outside the user's scope.</exception>
    /// <exception cref="TicketActionForbiddenException">A client writing an internal comment.</exception>
    public async Task<CommentResponse> AddAsync(
        Guid ticketId,
        CreateCommentRequest request,
        Guid authorId,
        UserRole role,
        CancellationToken cancellationToken)
    {
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);
        var ticket = await LoadVisibleAsync(ticketId, authorId, role, cancellationToken);

        // Checked after the lookup, so a ticket outside the client's scope answers 404 whatever the body says.
        var isInternal = request.IsInternal!.Value;
        if (isInternal && CommentVisibilityFor(role) != CommentVisibility.IncludeInternal)
        {
            throw new TicketActionForbiddenException("Only support staff can write internal comments.");
        }

        var comment = ticket.CreateComment(authorId, request.Content, isInternal, timeProvider.GetUtcNow());
        var author = await users.GetByIdAsync(authorId, cancellationToken)
            ?? throw new InvalidOperationException("The authenticated user does not exist.");

        // No audit entry: the comment is its own record, and the ticket itself does not change.
        comments.Add(comment);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CommentResponse(
            comment.Id,
            comment.TicketId,
            comment.AuthorId,
            author.Name,
            comment.Content,
            comment.IsInternal,
            comment.CreatedAt);
    }

    /// <param name="userId">The authenticated user; decides, with the role, which tickets they may read.</param>
    /// <param name="role">The authenticated user's role; clients only get public comments.</param>
    /// <exception cref="TicketNotFoundException">The ticket does not exist or is outside the user's scope.</exception>
    public async Task<IReadOnlyList<CommentResponse>> ListAsync(
        Guid ticketId,
        Guid userId,
        UserRole role,
        CancellationToken cancellationToken)
    {
        await LoadVisibleAsync(ticketId, userId, role, cancellationToken);
        return await comments.ListAsync(ticketId, CommentVisibilityFor(role), cancellationToken);
    }

    private async Task<Ticket> LoadVisibleAsync(Guid ticketId, Guid userId, UserRole role, CancellationToken cancellationToken) =>
        await tickets.GetVisibleAsync(ticketId, TicketVisibilities.For(role), userId, cancellationToken)
        ?? throw new TicketNotFoundException();

    // As with tickets, an unknown role must never widen what is shown, so it fails.
    private static CommentVisibility CommentVisibilityFor(UserRole role) => role switch
    {
        UserRole.Admin or UserRole.Agent => CommentVisibility.IncludeInternal,
        UserRole.Client => CommentVisibility.PublicOnly,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown role.")
    };
}
