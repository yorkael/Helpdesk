namespace Helpdesk.Application.Tickets;

/// <summary>
/// One audit entry, with values as stored: status names, and user ids for the assignee. For the assignee,
/// <see cref="OldValueName"/> and <see cref="NewValueName"/> carry the users' names, or null when an id matches
/// no user; they are always null for other fields. Carries user names but never emails.
/// </summary>
public sealed record TicketHistoryEntryResponse(
    Guid Id,
    string Field,
    string? OldValue,
    string? NewValue,
    string? OldValueName,
    string? NewValueName,
    Guid ChangedById,
    string ChangedByName,
    DateTimeOffset ChangedAt);
