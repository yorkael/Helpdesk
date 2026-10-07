using Helpdesk.Application.Abstractions;
using Helpdesk.Application.Tickets;
using Helpdesk.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.Infrastructure.Persistence.Repositories;

internal sealed class CommentRepository(HelpdeskDbContext context) : ICommentRepository
{
    public void Add(Comment comment) => context.Comments.Add(comment);

    /// <summary>
    /// One query, with the author's name joined in SQL. Internal comments are filtered in that query, so they
    /// never leave the database for a caller who may not see them. Projecting to a non-entity type tracks nothing.
    /// </summary>
    public async Task<IReadOnlyList<CommentResponse>> ListAsync(
        Guid ticketId,
        CommentVisibility visibility,
        CancellationToken cancellationToken) =>
        await VisibleTo(visibility)
            .Where(comment => comment.TicketId == ticketId)
            // Id breaks ties between comments written at the same instant, so the order is always the same.
            .OrderBy(comment => comment.CreatedAt)
            .ThenBy(comment => comment.Id)
            .Select(comment => new CommentResponse(
                comment.Id,
                comment.TicketId,
                comment.AuthorId,
                comment.Author.Name,
                comment.Content,
                comment.IsInternal,
                comment.CreatedAt))
            .ToListAsync(cancellationToken);

    private IQueryable<Comment> VisibleTo(CommentVisibility visibility) => visibility switch
    {
        CommentVisibility.IncludeInternal => context.Comments,
        CommentVisibility.PublicOnly => context.Comments.Where(comment => !comment.IsInternal),
        _ => throw new ArgumentOutOfRangeException(nameof(visibility), visibility, "Unknown visibility.")
    };
}
