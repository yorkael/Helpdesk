using Helpdesk.Domain.Enums;
using Helpdesk.Domain.Exceptions;

namespace Helpdesk.Domain.Entities;

public class Ticket
{
    private readonly List<Comment> _comments = [];

    public Ticket(
        string title,
        string description,
        TicketPriority priority,
        Guid categoryId,
        Guid createdById,
        DateTimeOffset createdAt)
    {
        Id = Guid.CreateVersion7();
        Title = Guard.NotBlank(title, nameof(title));
        Description = Guard.NotBlank(description, nameof(description));
        Status = TicketStatus.Open;
        Priority = Guard.Defined(priority, nameof(priority));
        CategoryId = Guard.NotEmpty(categoryId, nameof(categoryId));
        CreatedById = Guard.NotEmpty(createdById, nameof(createdById));
        CreatedAt = Guard.Utc(createdAt, nameof(createdAt));
    }

    private Ticket()
    {
    }

    public Guid Id { get; private set; }

    public string Title { get; private set; } = null!;

    public string Description { get; private set; } = null!;

    public TicketStatus Status { get; private set; }

    public TicketPriority Priority { get; private set; }

    public Guid CategoryId { get; private set; }

    public Category Category { get; private set; } = null!;

    public Guid CreatedById { get; private set; }

    public User CreatedBy { get; private set; } = null!;

    public Guid? AssignedToId { get; private set; }

    public User? AssignedTo { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyCollection<Comment> Comments => _comments.AsReadOnly();

    // Optimistic concurrency token; persistence maps it to PostgreSQL's xmin system column.
    public uint Version { get; private set; }

    /// <returns>The audit entry for the change, or null when the ticket already has this status.</returns>
    /// <exception cref="TicketRuleViolationException">
    /// The transition is not in <see cref="TicketStatusTransitions"/>, or the new status needs an assignee.
    /// </exception>
    public AuditLog? ChangeStatus(TicketStatus status, Guid actorId, DateTimeOffset changedAt)
    {
        Guard.Defined(status, nameof(status));
        Guard.NotEmpty(actorId, nameof(actorId));
        Guard.Utc(changedAt, nameof(changedAt));

        if (status == Status)
        {
            return null;
        }

        if (!TicketStatusTransitions.IsAllowed(Status, status))
        {
            throw new TicketRuleViolationException($"Cannot change status from {Status} to {status}.");
        }

        if (AssignedToId is null && TicketStatusTransitions.RequiresAssignee(status))
        {
            throw new TicketRuleViolationException($"Assign the ticket before moving it to {status}.");
        }

        var previousStatus = Status;
        Status = status;
        return AuditLog.StatusChanged(this, previousStatus, actorId, changedAt);
    }

    /// <summary>Changes only the assignee; taking a ticket does not move its status.</summary>
    /// <returns>The audit entry for the change, or null when the ticket is already assigned to this user.</returns>
    /// <exception cref="TicketRuleViolationException">The ticket is closed.</exception>
    public AuditLog? Assign(Guid assigneeId, Guid actorId, DateTimeOffset changedAt)
    {
        Guard.NotEmpty(assigneeId, nameof(assigneeId));
        Guard.NotEmpty(actorId, nameof(actorId));
        Guard.Utc(changedAt, nameof(changedAt));

        if (assigneeId == AssignedToId)
        {
            return null;
        }

        if (Status == TicketStatus.Closed)
        {
            throw new TicketRuleViolationException("A closed ticket cannot be assigned.");
        }

        var previousAssigneeId = AssignedToId;
        AssignedToId = assigneeId;
        return AuditLog.AssigneeChanged(this, previousAssigneeId, actorId, changedAt);
    }
}
