using Helpdesk.Domain.Entities;

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
    public void Blank_field_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new AuditLog(Guid.NewGuid(), Guid.NewGuid(), "", "a", "b", UtcNow));
    }
}
