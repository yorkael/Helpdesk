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

    public Guid Id { get; private set; }

    public Guid TicketId { get; private set; }

    public Guid UserId { get; private set; }

    public string Field { get; private set; } = null!;

    public string? OldValue { get; private set; }

    public string? NewValue { get; private set; }

    public DateTimeOffset ChangedAt { get; private set; }
}
