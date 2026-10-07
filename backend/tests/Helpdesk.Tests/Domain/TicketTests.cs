using Helpdesk.Domain.Entities;
using Helpdesk.Domain.Enums;
using Helpdesk.Domain.Exceptions;

namespace Helpdesk.Tests.Domain;

public class TicketTests
{
    private static readonly DateTimeOffset UtcNow = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ChangedAt = UtcNow.AddHours(1);
    private static readonly Guid AgentId = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();

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

    [Theory]
    [InlineData(TicketStatus.Open, TicketStatus.InProgress)]
    [InlineData(TicketStatus.Open, TicketStatus.Closed)]
    [InlineData(TicketStatus.InProgress, TicketStatus.WaitingOnCustomer)]
    [InlineData(TicketStatus.InProgress, TicketStatus.Resolved)]
    [InlineData(TicketStatus.WaitingOnCustomer, TicketStatus.InProgress)]
    [InlineData(TicketStatus.WaitingOnCustomer, TicketStatus.Resolved)]
    [InlineData(TicketStatus.Resolved, TicketStatus.InProgress)]
    [InlineData(TicketStatus.Resolved, TicketStatus.Closed)]
    public void Allowed_status_change_is_applied_and_recorded_with_old_and_new_values(TicketStatus from, TicketStatus to)
    {
        var ticket = AssignedTicketIn(from);

        var entry = ticket.ChangeStatus(to, ActorId, ChangedAt);

        Assert.Equal(to, ticket.Status);
        Assert.NotNull(entry);
        Assert.Equal(ticket.Id, entry.TicketId);
        Assert.Equal(ActorId, entry.UserId);
        Assert.Equal("Status", entry.Field);
        Assert.Equal(from.ToString(), entry.OldValue);
        Assert.Equal(to.ToString(), entry.NewValue);
        Assert.Equal(ChangedAt, entry.ChangedAt);
    }

    [Theory]
    [InlineData(TicketStatus.Open, TicketStatus.WaitingOnCustomer)]
    [InlineData(TicketStatus.Open, TicketStatus.Resolved)]
    [InlineData(TicketStatus.InProgress, TicketStatus.Open)]
    [InlineData(TicketStatus.InProgress, TicketStatus.Closed)]
    [InlineData(TicketStatus.WaitingOnCustomer, TicketStatus.Open)]
    [InlineData(TicketStatus.WaitingOnCustomer, TicketStatus.Closed)]
    [InlineData(TicketStatus.Resolved, TicketStatus.Open)]
    [InlineData(TicketStatus.Resolved, TicketStatus.WaitingOnCustomer)]
    [InlineData(TicketStatus.Closed, TicketStatus.Open)]
    [InlineData(TicketStatus.Closed, TicketStatus.InProgress)]
    [InlineData(TicketStatus.Closed, TicketStatus.WaitingOnCustomer)]
    [InlineData(TicketStatus.Closed, TicketStatus.Resolved)]
    public void Disallowed_status_change_is_rejected_with_both_statuses_named(TicketStatus from, TicketStatus to)
    {
        var ticket = AssignedTicketIn(from);

        var exception = Assert.Throws<TicketRuleViolationException>(() => ticket.ChangeStatus(to, ActorId, ChangedAt));

        Assert.Equal($"Cannot change status from {from} to {to}.", exception.Message);
        Assert.Equal(from, ticket.Status);
    }

    [Theory]
    [InlineData(TicketStatus.Open)]
    [InlineData(TicketStatus.InProgress)]
    [InlineData(TicketStatus.WaitingOnCustomer)]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    public void Changing_to_the_current_status_records_nothing(TicketStatus status)
    {
        var ticket = AssignedTicketIn(status);

        Assert.Null(ticket.ChangeStatus(status, ActorId, ChangedAt));
        Assert.Equal(status, ticket.Status);
    }

    [Fact]
    public void Unassigned_ticket_cannot_be_moved_to_in_progress()
    {
        var ticket = NewTicket();

        var exception = Assert.Throws<TicketRuleViolationException>(() =>
            ticket.ChangeStatus(TicketStatus.InProgress, ActorId, ChangedAt));

        Assert.Equal("Assign the ticket before moving it to InProgress.", exception.Message);
        Assert.Equal(TicketStatus.Open, ticket.Status);
    }

    [Fact]
    public void Unassigned_ticket_can_be_closed()
    {
        var ticket = NewTicket();

        var entry = ticket.ChangeStatus(TicketStatus.Closed, ActorId, ChangedAt);

        Assert.Equal(TicketStatus.Closed, ticket.Status);
        Assert.Null(ticket.AssignedToId);
        Assert.Equal("Closed", entry?.NewValue);
    }

    [Fact]
    public void Status_change_rejects_an_undefined_status()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AssignedTicketIn(TicketStatus.Open).ChangeStatus((TicketStatus)99, ActorId, ChangedAt));
    }

    [Fact]
    public void Status_change_rejects_an_empty_actor()
    {
        Assert.Throws<ArgumentException>(() =>
            AssignedTicketIn(TicketStatus.Open).ChangeStatus(TicketStatus.InProgress, Guid.Empty, ChangedAt));
    }

    [Fact]
    public void Status_change_rejects_a_non_utc_time()
    {
        Assert.Throws<ArgumentException>(() =>
            AssignedTicketIn(TicketStatus.Open).ChangeStatus(TicketStatus.InProgress, ActorId, ChangedAt.ToOffset(TimeSpan.FromHours(-5))));
    }

    [Fact]
    public void First_assignment_is_recorded_without_an_old_value()
    {
        var ticket = NewTicket();

        var entry = ticket.Assign(AgentId, ActorId, ChangedAt);

        Assert.Equal(AgentId, ticket.AssignedToId);
        Assert.NotNull(entry);
        Assert.Equal(ticket.Id, entry.TicketId);
        Assert.Equal(ActorId, entry.UserId);
        Assert.Equal("AssignedToId", entry.Field);
        Assert.Null(entry.OldValue);
        Assert.Equal(AgentId.ToString(), entry.NewValue);
        Assert.Equal(ChangedAt, entry.ChangedAt);
    }

    [Theory]
    [InlineData(TicketStatus.Open)]
    [InlineData(TicketStatus.InProgress)]
    [InlineData(TicketStatus.WaitingOnCustomer)]
    [InlineData(TicketStatus.Resolved)]
    public void Reassignment_is_recorded_with_old_and_new_assignees_and_keeps_the_status(TicketStatus status)
    {
        var ticket = AssignedTicketIn(status);
        var otherAgentId = Guid.NewGuid();

        var entry = ticket.Assign(otherAgentId, ActorId, ChangedAt);

        Assert.Equal(otherAgentId, ticket.AssignedToId);
        Assert.Equal(status, ticket.Status);
        Assert.Equal(AgentId.ToString(), entry?.OldValue);
        Assert.Equal(otherAgentId.ToString(), entry?.NewValue);
    }

    [Fact]
    public void Assigning_does_not_move_an_open_ticket()
    {
        var ticket = NewTicket();

        ticket.Assign(AgentId, ActorId, ChangedAt);

        Assert.Equal(TicketStatus.Open, ticket.Status);
    }

    [Fact]
    public void Assigning_the_current_assignee_records_nothing()
    {
        var ticket = AssignedTicketIn(TicketStatus.InProgress);

        Assert.Null(ticket.Assign(AgentId, ActorId, ChangedAt));
        Assert.Equal(AgentId, ticket.AssignedToId);
    }

    [Fact]
    public void Closed_ticket_cannot_be_reassigned()
    {
        var ticket = AssignedTicketIn(TicketStatus.Closed);

        var exception = Assert.Throws<TicketRuleViolationException>(() =>
            ticket.Assign(Guid.NewGuid(), ActorId, ChangedAt));

        Assert.Equal("A closed ticket cannot be assigned.", exception.Message);
        Assert.Equal(AgentId, ticket.AssignedToId);
    }

    [Fact]
    public void Closed_ticket_assigned_to_the_same_agent_is_not_a_violation()
    {
        var ticket = AssignedTicketIn(TicketStatus.Closed);

        Assert.Null(ticket.Assign(AgentId, ActorId, ChangedAt));
    }

    [Fact]
    public void Unassigned_closed_ticket_cannot_be_assigned()
    {
        var ticket = NewTicket();
        ticket.ChangeStatus(TicketStatus.Closed, ActorId, ChangedAt);

        Assert.Throws<TicketRuleViolationException>(() => ticket.Assign(AgentId, ActorId, ChangedAt));
        Assert.Null(ticket.AssignedToId);
    }

    [Fact]
    public void Assignment_rejects_empty_ids()
    {
        var ticket = NewTicket();

        Assert.Throws<ArgumentException>(() => ticket.Assign(Guid.Empty, ActorId, ChangedAt));
        Assert.Throws<ArgumentException>(() => ticket.Assign(AgentId, Guid.Empty, ChangedAt));
        Assert.Null(ticket.AssignedToId);
    }

    [Fact]
    public void Assignment_rejects_a_non_utc_time()
    {
        Assert.Throws<ArgumentException>(() =>
            NewTicket().Assign(AgentId, ActorId, ChangedAt.ToOffset(TimeSpan.FromHours(-5))));
    }

    [Theory]
    [InlineData(TicketStatus.Open)]
    [InlineData(TicketStatus.InProgress)]
    [InlineData(TicketStatus.WaitingOnCustomer)]
    [InlineData(TicketStatus.Resolved)]
    public void Ticket_that_is_not_closed_accepts_comments_without_changing(TicketStatus status)
    {
        var ticket = AssignedTicketIn(status);

        var comment = ticket.CreateComment(ActorId, "Any update?", isInternal: false, ChangedAt);

        Assert.Equal(ticket.Id, comment.TicketId);
        Assert.Equal(status, ticket.Status);
        Assert.Equal(AgentId, ticket.AssignedToId);
        Assert.Empty(ticket.Comments);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Closed_ticket_cannot_receive_comments(bool isInternal)
    {
        var ticket = AssignedTicketIn(TicketStatus.Closed);

        var exception = Assert.Throws<TicketRuleViolationException>(() =>
            ticket.CreateComment(ActorId, "Any update?", isInternal, ChangedAt));

        Assert.Equal("A closed ticket cannot receive comments.", exception.Message);
        Assert.Equal(TicketStatus.Closed, ticket.Status);
    }

    [Fact]
    public void Invalid_comment_on_a_closed_ticket_is_reported_as_an_invalid_argument()
    {
        var ticket = AssignedTicketIn(TicketStatus.Closed);

        Assert.Throws<ArgumentException>(() => ticket.CreateComment(Guid.Empty, "Any update?", false, ChangedAt));
    }

    private static Ticket NewTicket() =>
        new("Printer offline", "It shows error 42.", TicketPriority.High, Guid.NewGuid(), Guid.NewGuid(), UtcNow);

    // Reaches the status through allowed transitions only, so no test depends on setting state directly.
    private static Ticket AssignedTicketIn(TicketStatus status)
    {
        var ticket = NewTicket();
        ticket.Assign(AgentId, ActorId, UtcNow);

        TicketStatus[] path = status switch
        {
            TicketStatus.Open => [],
            TicketStatus.InProgress => [TicketStatus.InProgress],
            TicketStatus.WaitingOnCustomer => [TicketStatus.InProgress, TicketStatus.WaitingOnCustomer],
            TicketStatus.Resolved => [TicketStatus.InProgress, TicketStatus.Resolved],
            TicketStatus.Closed => [TicketStatus.InProgress, TicketStatus.Resolved, TicketStatus.Closed],
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };

        foreach (var step in path)
        {
            ticket.ChangeStatus(step, ActorId, UtcNow);
        }

        return ticket;
    }
}
