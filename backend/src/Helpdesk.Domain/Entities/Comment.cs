namespace Helpdesk.Domain.Entities;

public class Comment
{
    public Comment(Guid ticketId, Guid authorId, string content, bool isInternal, DateTimeOffset createdAt)
    {
        Id = Guid.CreateVersion7();
        TicketId = Guard.NotEmpty(ticketId, nameof(ticketId));
        AuthorId = Guard.NotEmpty(authorId, nameof(authorId));
        Content = Guard.NotBlank(content, nameof(content));
        IsInternal = isInternal;
        CreatedAt = Guard.Utc(createdAt, nameof(createdAt));
    }

    private Comment()
    {
    }

    public Guid Id { get; private set; }

    public Guid TicketId { get; private set; }

    public Guid AuthorId { get; private set; }

    public User Author { get; private set; } = null!;

    public string Content { get; private set; } = null!;

    public bool IsInternal { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
