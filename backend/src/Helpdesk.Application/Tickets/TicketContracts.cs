namespace Helpdesk.Application.Tickets;

/// <summary>
/// Has no assignee or status on purpose: a client cannot set them, and unknown JSON members are ignored.
/// </summary>
public sealed record CreateTicketRequest(string Title, string Description, Guid CategoryId, string Priority);

/// <summary>Only the new assignee: the user making the change comes from the access token, never from the body.</summary>
public sealed record AssignTicketRequest(Guid AssigneeId);

/// <summary>Only the new status: the user making the change comes from the access token, never from the body.</summary>
public sealed record ChangeTicketStatusRequest(string Status);

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

/// <summary>
/// Built from the query string. Has no creator filter on purpose: which tickets a user may see
/// comes from their token, and unknown query parameters are ignored.
/// </summary>
public sealed record ListTicketsRequest(
    int? Page,
    int? PageSize,
    string? Status,
    string? Priority,
    Guid? AssignedToId,
    Guid? CategoryId,
    string? Search);

public sealed record TicketListItem(
    Guid Id,
    string Title,
    string Status,
    string Priority,
    Guid CategoryId,
    string CategoryName,
    Guid CreatedById,
    string CreatedByName,
    Guid? AssignedToId,
    string? AssignedToName,
    DateTimeOffset CreatedAt);

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
