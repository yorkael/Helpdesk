namespace Helpdesk.Application.Tickets;

/// <summary>
/// Has no assignee or status on purpose: a client cannot set them, and unknown JSON members are ignored.
/// </summary>
public sealed record CreateTicketRequest(string Title, string Description, Guid CategoryId, string Priority);

public sealed record TicketResponse(
    Guid Id,
    string Title,
    string Description,
    string Status,
    string Priority,
    Guid CategoryId,
    Guid CreatedById,
    Guid? AssignedToId,
    DateTimeOffset CreatedAt);
