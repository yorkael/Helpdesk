using Helpdesk.Application.Tickets;
using Helpdesk.Domain.Entities;

namespace Helpdesk.Application.Abstractions;

public interface ICommentRepository
{
    void Add(Comment comment);

    /// <returns>The ticket's comments allowed by <paramref name="visibility"/>, oldest first.</returns>
    Task<IReadOnlyList<CommentResponse>> ListAsync(
        Guid ticketId,
        CommentVisibility visibility,
        CancellationToken cancellationToken);
}
