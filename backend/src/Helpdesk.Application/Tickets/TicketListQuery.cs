using Helpdesk.Domain.Enums;

namespace Helpdesk.Application.Tickets;

/// <summary>Which tickets a user may list; decided from their role, applied by the repository.</summary>
public enum TicketVisibility
{
    All,
    CreatedByUser,
    AssignedToUserOrUnassigned
}

/// <param name="Search">Trimmed text to find in the title or description; null when there is none.</param>
public sealed record TicketListQuery(
    TicketVisibility Visibility,
    Guid UserId,
    TicketStatus? Status,
    TicketPriority? Priority,
    Guid? AssignedToId,
    Guid? CategoryId,
    string? Search,
    int Page,
    int PageSize);
