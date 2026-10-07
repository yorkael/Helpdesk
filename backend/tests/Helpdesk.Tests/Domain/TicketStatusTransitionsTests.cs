using Helpdesk.Domain.Entities;
using Helpdesk.Domain.Enums;

namespace Helpdesk.Tests.Domain;

public class TicketStatusTransitionsTests
{
    // Every pair is written out by hand instead of read from the table, so a wrong edit to the table fails here.
    [Theory]
    [InlineData(TicketStatus.Open, TicketStatus.Open, false)]
    [InlineData(TicketStatus.Open, TicketStatus.InProgress, true)]
    [InlineData(TicketStatus.Open, TicketStatus.WaitingOnCustomer, false)]
    [InlineData(TicketStatus.Open, TicketStatus.Resolved, false)]
    [InlineData(TicketStatus.Open, TicketStatus.Closed, true)]
    [InlineData(TicketStatus.InProgress, TicketStatus.Open, false)]
    [InlineData(TicketStatus.InProgress, TicketStatus.InProgress, false)]
    [InlineData(TicketStatus.InProgress, TicketStatus.WaitingOnCustomer, true)]
    [InlineData(TicketStatus.InProgress, TicketStatus.Resolved, true)]
    [InlineData(TicketStatus.InProgress, TicketStatus.Closed, false)]
    [InlineData(TicketStatus.WaitingOnCustomer, TicketStatus.Open, false)]
    [InlineData(TicketStatus.WaitingOnCustomer, TicketStatus.InProgress, true)]
    [InlineData(TicketStatus.WaitingOnCustomer, TicketStatus.WaitingOnCustomer, false)]
    [InlineData(TicketStatus.WaitingOnCustomer, TicketStatus.Resolved, true)]
    [InlineData(TicketStatus.WaitingOnCustomer, TicketStatus.Closed, false)]
    [InlineData(TicketStatus.Resolved, TicketStatus.Open, false)]
    [InlineData(TicketStatus.Resolved, TicketStatus.InProgress, true)]
    [InlineData(TicketStatus.Resolved, TicketStatus.WaitingOnCustomer, false)]
    [InlineData(TicketStatus.Resolved, TicketStatus.Resolved, false)]
    [InlineData(TicketStatus.Resolved, TicketStatus.Closed, true)]
    [InlineData(TicketStatus.Closed, TicketStatus.Open, false)]
    [InlineData(TicketStatus.Closed, TicketStatus.InProgress, false)]
    [InlineData(TicketStatus.Closed, TicketStatus.WaitingOnCustomer, false)]
    [InlineData(TicketStatus.Closed, TicketStatus.Resolved, false)]
    [InlineData(TicketStatus.Closed, TicketStatus.Closed, false)]
    public void Only_the_listed_transitions_are_allowed(TicketStatus from, TicketStatus to, bool allowed)
    {
        Assert.Equal(allowed, TicketStatusTransitions.IsAllowed(from, to));
    }

    [Theory]
    [InlineData(TicketStatus.Open, false)]
    [InlineData(TicketStatus.InProgress, true)]
    [InlineData(TicketStatus.WaitingOnCustomer, true)]
    [InlineData(TicketStatus.Resolved, true)]
    [InlineData(TicketStatus.Closed, false)]
    public void Only_working_statuses_require_an_assignee(TicketStatus status, bool required)
    {
        Assert.Equal(required, TicketStatusTransitions.RequiresAssignee(status));
    }

    [Fact]
    public void Undefined_status_has_no_transitions()
    {
        Assert.False(TicketStatusTransitions.IsAllowed((TicketStatus)99, TicketStatus.Open));
    }
}
