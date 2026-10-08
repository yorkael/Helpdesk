using Helpdesk.Application.Abstractions;
using Helpdesk.Domain.Entities;
using Helpdesk.Domain.Enums;

namespace Helpdesk.Application.Tickets;

/// <summary>
/// Reads a ticket's audit history. Admins only. Checks run in this order: role (403), ticket (404).
/// Read only: nothing is saved, and reads are not audited.
/// </summary>
public sealed class TicketHistoryService(
    ITicketRepository tickets,
    IAuditLogRepository auditLogs,
    IUserRepository users)
{
    /// <param name="userId">The authenticated user; decides, with the role, which tickets they may read.</param>
    /// <param name="role">The authenticated user's role; only admins may read the history.</param>
    /// <returns>The ticket's entries, oldest first.</returns>
    /// <exception cref="TicketActionForbiddenException">The caller is not an admin.</exception>
    /// <exception cref="TicketNotFoundException">The ticket does not exist.</exception>
    public async Task<IReadOnlyList<TicketHistoryEntryResponse>> ListAsync(
        Guid ticketId,
        Guid userId,
        UserRole role,
        CancellationToken cancellationToken)
    {
        EnsureAdmin(role);
        await LoadVisibleAsync(ticketId, userId, role, cancellationToken);

        var entries = await auditLogs.ListByTicketAsync(ticketId, cancellationToken);
        if (entries.Count == 0)
        {
            return [];
        }

        // One lookup for every user the history mentions, whether they made a change or were assigned.
        var userIds = entries
            .Select(entry => entry.UserId)
            .Concat(entries.SelectMany(AssigneeIds))
            .ToHashSet();
        var names = await users.GetNamesAsync(userIds, cancellationToken);

        return entries.Select(entry => ToResponse(entry, names)).ToList();
    }

    // The API's admin policy already stops everyone else; repeated here so the service is safe on its own.
    private static void EnsureAdmin(UserRole role)
    {
        if (role != UserRole.Admin)
        {
            throw new TicketActionForbiddenException("Only admins can read the ticket history.");
        }
    }

    // Scoped by role like the other ticket services, so widening who may read the history keeps each role's scope.
    private async Task<Ticket> LoadVisibleAsync(Guid ticketId, Guid userId, UserRole role, CancellationToken cancellationToken) =>
        await tickets.GetVisibleAsync(ticketId, TicketVisibilities.For(role), userId, cancellationToken)
        ?? throw new TicketNotFoundException();

    private static IEnumerable<Guid> AssigneeIds(AuditLog entry)
    {
        if (entry.Field != AuditFields.Assignee)
        {
            yield break;
        }

        if (Guid.TryParse(entry.OldValue, out var oldId))
        {
            yield return oldId;
        }

        if (Guid.TryParse(entry.NewValue, out var newId))
        {
            yield return newId;
        }
    }

    private static TicketHistoryEntryResponse ToResponse(AuditLog entry, IReadOnlyDictionary<Guid, string> names)
    {
        var isAssignee = entry.Field == AuditFields.Assignee;

        // The foreign key keeps every author in the database, so a missing one is a bug, not a gap in the data.
        var changedByName = names.GetValueOrDefault(entry.UserId)
            ?? throw new InvalidOperationException("An audit entry refers to a user that does not exist.");

        return new TicketHistoryEntryResponse(
            entry.Id,
            entry.Field,
            entry.OldValue,
            entry.NewValue,
            isAssignee ? NameOf(entry.OldValue, names) : null,
            isAssignee ? NameOf(entry.NewValue, names) : null,
            entry.UserId,
            changedByName,
            entry.ChangedAt);
    }

    private static string? NameOf(string? userId, IReadOnlyDictionary<Guid, string> names) =>
        Guid.TryParse(userId, out var id) ? names.GetValueOrDefault(id) : null;
}
