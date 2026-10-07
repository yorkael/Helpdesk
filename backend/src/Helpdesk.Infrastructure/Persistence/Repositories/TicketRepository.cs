using Helpdesk.Application.Abstractions;
using Helpdesk.Application.Tickets;
using Helpdesk.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.Infrastructure.Persistence.Repositories;

internal sealed class TicketRepository(HelpdeskDbContext context) : ITicketRepository
{
    private const string LikeEscapeCharacter = @"\";

    public void Add(Ticket ticket) => context.Tickets.Add(ticket);

    /// <summary>
    /// Two queries per call: a count and one page projected straight to the list item, with the names
    /// joined in SQL. Projecting to a non-entity type means nothing is tracked.
    /// </summary>
    public async Task<PagedResponse<TicketListItem>> ListAsync(TicketListQuery query, CancellationToken cancellationToken)
    {
        var tickets = Filter(query);

        var totalCount = await tickets.CountAsync(cancellationToken);

        // Id breaks ties between tickets created at the same instant, so pages never overlap or skip rows.
        var items = await tickets
            .OrderByDescending(ticket => ticket.CreatedAt)
            .ThenByDescending(ticket => ticket.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(ticket => new TicketListItem(
                ticket.Id,
                ticket.Title,
                ticket.Status.ToString(),
                ticket.Priority.ToString(),
                ticket.CategoryId,
                ticket.Category.Name,
                ticket.CreatedById,
                ticket.CreatedBy.Name,
                ticket.AssignedToId,
                ticket.AssignedTo != null ? ticket.AssignedTo.Name : null,
                ticket.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResponse<TicketListItem>(items, query.Page, query.PageSize, totalCount);
    }

    /// <summary>
    /// Tracked, so the caller's changes are saved and checked against the row version it read.
    /// Not a SELECT ... FOR UPDATE: nothing is locked; a concurrent change is detected when saving.
    /// </summary>
    public Task<Ticket?> GetVisibleAsync(
        Guid id,
        TicketVisibility visibility,
        Guid userId,
        CancellationToken cancellationToken) =>
        VisibleTo(visibility, userId).SingleOrDefaultAsync(ticket => ticket.Id == id, cancellationToken);

    // The one place that decides which tickets a user may see; listing and loading one ticket both use it.
    private IQueryable<Ticket> VisibleTo(TicketVisibility visibility, Guid userId) => visibility switch
    {
        TicketVisibility.All => context.Tickets,
        TicketVisibility.CreatedByUser => context.Tickets.Where(ticket => ticket.CreatedById == userId),
        TicketVisibility.AssignedToUserOrUnassigned => context.Tickets.Where(ticket =>
            ticket.AssignedToId == userId || ticket.AssignedToId == null),
        _ => throw new ArgumentOutOfRangeException(nameof(visibility), visibility, "Unknown visibility.")
    };

    private IQueryable<Ticket> Filter(TicketListQuery query)
    {
        var tickets = VisibleTo(query.Visibility, query.UserId);

        if (query.Status is { } status)
        {
            tickets = tickets.Where(ticket => ticket.Status == status);
        }

        if (query.Priority is { } priority)
        {
            tickets = tickets.Where(ticket => ticket.Priority == priority);
        }

        if (query.AssignedToId is { } assignedToId)
        {
            tickets = tickets.Where(ticket => ticket.AssignedToId == assignedToId);
        }

        if (query.CategoryId is { } categoryId)
        {
            tickets = tickets.Where(ticket => ticket.CategoryId == categoryId);
        }

        if (query.Search is { } search)
        {
            var pattern = $"%{EscapeLikeWildcards(search)}%";
            tickets = tickets.Where(ticket =>
                EF.Functions.ILike(ticket.Title, pattern, LikeEscapeCharacter) ||
                EF.Functions.ILike(ticket.Description, pattern, LikeEscapeCharacter));
        }

        return tickets;
    }

    // The escape character goes first, so the escapes added for % and _ are not escaped again.
    private static string EscapeLikeWildcards(string text) => text
        .Replace(LikeEscapeCharacter, LikeEscapeCharacter + LikeEscapeCharacter)
        .Replace("%", LikeEscapeCharacter + "%")
        .Replace("_", LikeEscapeCharacter + "_");
}
