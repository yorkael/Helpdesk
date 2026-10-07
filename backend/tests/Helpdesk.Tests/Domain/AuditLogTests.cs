using Helpdesk.Domain.Entities;
using Helpdesk.Domain.Enums;

namespace Helpdesk.Tests.Domain;

public class AuditLogTests
{
    private static readonly DateTimeOffset UtcNow = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Creation_entry_has_no_old_value()
    {
        var entry = new AuditLog(Guid.NewGuid(), Guid.NewGuid(), "Status", null, "Open", UtcNow);

        Assert.Null(entry.OldValue);
        Assert.Equal("Open", entry.NewValue);
    }

    [Fact]
    public void Ticket_creation_is_recorded_as_the_first_status_by_its_creator_at_creation_time()
    {
        var ticket = new Ticket("Printer jam", "Paper stuck", TicketPriority.High, Guid.NewGuid(), Guid.NewGuid(), UtcNow);

        var entry = AuditLog.TicketCreated(ticket);

        Assert.Equal(ticket.Id, entry.TicketId);
        Assert.Equal(ticket.CreatedById, entry.UserId);
        Assert.Equal("Status", entry.Field);
        Assert.Null(entry.OldValue);
        Assert.Equal("Open", entry.NewValue);
        Assert.Equal(UtcNow, entry.ChangedAt);
    }

    [Fact]
    public void Blank_field_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new AuditLog(Guid.NewGuid(), Guid.NewGuid(), "", "a", "b", UtcNow));
    }
}
