using Helpdesk.Domain.Entities;
using Helpdesk.Domain.Enums;

namespace Helpdesk.Tests.Domain;

public class CommentTests
{
    private static readonly DateTimeOffset UtcNow = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);

    private readonly Ticket _ticket =
        new("Printer offline", "It shows error 42.", TicketPriority.High, Guid.NewGuid(), Guid.NewGuid(), UtcNow);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Comment_belongs_to_the_ticket_and_keeps_its_author_visibility_and_time(bool isInternal)
    {
        var authorId = Guid.NewGuid();

        var comment = _ticket.CreateComment(authorId, "Checked the logs.", isInternal, UtcNow);

        Assert.NotEqual(Guid.Empty, comment.Id);
        Assert.Equal(_ticket.Id, comment.TicketId);
        Assert.Equal(authorId, comment.AuthorId);
        Assert.Equal("Checked the logs.", comment.Content);
        Assert.Equal(isInternal, comment.IsInternal);
        Assert.Equal(UtcNow, comment.CreatedAt);
    }

    [Fact]
    public void Content_is_trimmed()
    {
        var comment = _ticket.CreateComment(Guid.NewGuid(), "  Checked the logs. ", false, UtcNow);

        Assert.Equal("Checked the logs.", comment.Content);
    }

    [Fact]
    public void Empty_author_id_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => _ticket.CreateComment(Guid.Empty, "Content", false, UtcNow));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_content_is_rejected(string content)
    {
        Assert.Throws<ArgumentException>(() => _ticket.CreateComment(Guid.NewGuid(), content, false, UtcNow));
    }

    [Fact]
    public void Non_utc_creation_time_is_rejected()
    {
        var localTime = new DateTimeOffset(2026, 10, 5, 7, 0, 0, TimeSpan.FromHours(-5));

        Assert.Throws<ArgumentException>(() => _ticket.CreateComment(Guid.NewGuid(), "Content", false, localTime));
    }
}
