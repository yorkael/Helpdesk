using Helpdesk.Domain.Entities;

namespace Helpdesk.Tests.Domain;

public class CommentTests
{
    private static readonly DateTimeOffset UtcNow = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Internal_comment_keeps_its_visibility()
    {
        var comment = new Comment(Guid.NewGuid(), Guid.NewGuid(), "Checked the logs.", isInternal: true, UtcNow);

        Assert.True(comment.IsInternal);
        Assert.Equal("Checked the logs.", comment.Content);
    }

    [Fact]
    public void Empty_ticket_id_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new Comment(Guid.Empty, Guid.NewGuid(), "Content", false, UtcNow));
    }

    [Fact]
    public void Empty_author_id_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new Comment(Guid.NewGuid(), Guid.Empty, "Content", false, UtcNow));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_content_is_rejected(string content)
    {
        Assert.Throws<ArgumentException>(() => new Comment(Guid.NewGuid(), Guid.NewGuid(), content, false, UtcNow));
    }

    [Fact]
    public void Non_utc_creation_time_is_rejected()
    {
        var localTime = new DateTimeOffset(2026, 10, 5, 7, 0, 0, TimeSpan.FromHours(-5));

        Assert.Throws<ArgumentException>(() => new Comment(Guid.NewGuid(), Guid.NewGuid(), "Content", false, localTime));
    }
}
