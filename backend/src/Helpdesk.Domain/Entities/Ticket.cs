using Helpdesk.Domain.Enums;

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
}
