using Helpdesk.Domain.Entities;
using Helpdesk.Domain.Enums;

namespace Helpdesk.Tests.Domain;

public class TicketTests
{
    private static readonly DateTimeOffset UtcNow = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void New_ticket_starts_open_and_unassigned()
    {
        var ticket = new Ticket("Printer offline", "It shows error 42.", TicketPriority.High, Guid.NewGuid(), Guid.NewGuid(), UtcNow);

        Assert.Equal(TicketStatus.Open, ticket.Status);
        Assert.Null(ticket.AssignedToId);
        Assert.NotEqual(Guid.Empty, ticket.Id);
        Assert.Empty(ticket.Comments);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_title_is_rejected(string title)
    {
        Assert.Throws<ArgumentException>(() =>
            new Ticket(title, "Description", TicketPriority.Low, Guid.NewGuid(), Guid.NewGuid(), UtcNow));
    }

    [Fact]
    public void Empty_category_id_is_rejected()
    {
        Assert.Throws<ArgumentException>(() =>
            new Ticket("Title", "Description", TicketPriority.Low, Guid.Empty, Guid.NewGuid(), UtcNow));
    }

    [Fact]
    public void Undefined_priority_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Ticket("Title", "Description", (TicketPriority)99, Guid.NewGuid(), Guid.NewGuid(), UtcNow));
    }

    [Fact]
    public void Non_utc_creation_time_is_rejected()
    {
        var localTime = new DateTimeOffset(2026, 10, 5, 7, 0, 0, TimeSpan.FromHours(-5));

        Assert.Throws<ArgumentException>(() =>
            new Ticket("Title", "Description", TicketPriority.Low, Guid.NewGuid(), Guid.NewGuid(), localTime));
    }
}
