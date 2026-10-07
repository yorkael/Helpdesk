using Helpdesk.Domain.Enums;

namespace Helpdesk.Domain.Entities;

public class AuditLog
{
    public AuditLog(
        Guid ticketId,
        Guid userId,
        string field,
        string? oldValue,
        string? newValue,
        DateTimeOffset changedAt)
    {
        Id = Guid.CreateVersion7();
        TicketId = Guard.NotEmpty(ticketId, nameof(ticketId));
        UserId = Guard.NotEmpty(userId, nameof(userId));
        Field = Guard.NotBlank(field, nameof(field));
        OldValue = oldValue;
        NewValue = newValue;
        ChangedAt = Guard.Utc(changedAt, nameof(changedAt));
    }

    private AuditLog()
    {
    }

    /// <summary>A creation is recorded as the first status value, so the status history starts with it.</summary>
    public static AuditLog TicketCreated(Ticket ticket) => new(
        ticket.Id,
        ticket.CreatedById,
        AuditFields.Status,
        oldValue: null,
        ticket.Status.ToString(),
        ticket.CreatedAt);

    // Internal: only Ticket creates change entries, so a change and its entry cannot be built apart.
    internal static AuditLog StatusChanged(
        Ticket ticket,
        TicketStatus previousStatus,
        Guid actorId,
        DateTimeOffset changedAt) => new(
        ticket.Id,
        actorId,
        AuditFields.Status,
        previousStatus.ToString(),
        ticket.Status.ToString(),
        changedAt);

    /// <summary>Stores user ids, not names: a name can change, an id always points to the same user.</summary>
    internal static AuditLog AssigneeChanged(
        Ticket ticket,
        Guid? previousAssigneeId,
        Guid actorId,
        DateTimeOffset changedAt) => new(
        ticket.Id,
        actorId,
        AuditFields.Assignee,
        previousAssigneeId?.ToString(),
        ticket.AssignedToId?.ToString(),
        changedAt);

    public Guid Id { get; private set; }

    public Guid TicketId { get; private set; }

    public Guid UserId { get; private set; }

    public string Field { get; private set; } = null!;

    public string? OldValue { get; private set; }

    public string? NewValue { get; private set; }

    public DateTimeOffset ChangedAt { get; private set; }
}
