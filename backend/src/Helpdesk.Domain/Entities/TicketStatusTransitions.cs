using Helpdesk.Domain.Enums;

namespace Helpdesk.Domain.Entities;

/// <summary>The status changes a ticket allows; anything not listed is rejected.</summary>
public static class TicketStatusTransitions
{
    private static readonly IReadOnlyDictionary<TicketStatus, TicketStatus[]> Allowed =
        new Dictionary<TicketStatus, TicketStatus[]>
        {
            [TicketStatus.Open] = [TicketStatus.InProgress, TicketStatus.Closed],
            [TicketStatus.InProgress] = [TicketStatus.WaitingOnCustomer, TicketStatus.Resolved],
            [TicketStatus.WaitingOnCustomer] = [TicketStatus.InProgress, TicketStatus.Resolved],
            [TicketStatus.Resolved] = [TicketStatus.InProgress, TicketStatus.Closed],
            [TicketStatus.Closed] = []
        };

    public static bool IsAllowed(TicketStatus from, TicketStatus to) =>
        Allowed.TryGetValue(from, out var targets) && targets.Contains(to);

    /// <summary>Statuses that mean someone is working on the ticket; Open and Closed need no assignee.</summary>
    public static bool RequiresAssignee(TicketStatus status) =>
        status is TicketStatus.InProgress or TicketStatus.WaitingOnCustomer or TicketStatus.Resolved;
}
