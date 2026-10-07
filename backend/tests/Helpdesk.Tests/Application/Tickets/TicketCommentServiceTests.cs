using FluentValidation;
using Helpdesk.Application.Tickets;
using Helpdesk.Domain.Entities;
using Helpdesk.Domain.Enums;
using Helpdesk.Domain.Exceptions;
using Helpdesk.Tests.Application.Authentication;

namespace Helpdesk.Tests.Application.Tickets;

public class TicketCommentServiceTests
{
    private readonly InMemoryTicketRepository _tickets = new();
    private readonly InMemoryCommentRepository _comments = new();
    private readonly InMemoryUserRepository _users = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 10, 7, 9, 30, 0, TimeSpan.Zero));
    private readonly TicketCommentService _commentService;
    private readonly User _client;
    private readonly User _agent;
    private readonly User _admin;

    public TicketCommentServiceTests()
    {
        _commentService = new TicketCommentService(
            _tickets,
            _comments,
            _users,
            _unitOfWork,
            new CreateCommentRequestValidator(),
            _time);

        _client = SeedUser("Ana Client", "ana@example.com", UserRole.Client);
        _agent = SeedUser("Eva Agent", "eva@example.com", UserRole.Agent);
        _admin = SeedUser("Admin User", "admin@example.com", UserRole.Admin);
    }

    [Fact]
    public async Task Client_adds_a_public_comment_to_their_ticket_in_one_save()
    {
        var ticket = SeedTicket();

        var response = await _commentService.AddAsync(
            ticket.Id, new CreateCommentRequest("Any update?", false), _client.Id, UserRole.Client, CancellationToken.None);

        var comment = Assert.Single(_comments.Comments);
        Assert.Equal(
            new CommentResponse(comment.Id, ticket.Id, _client.Id, "Ana Client", "Any update?", false, _time.Now),
            response);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Author_is_the_authenticated_user_passed_in()
    {
        var ticket = SeedTicket();

        var response = await _commentService.AddAsync(
            ticket.Id, new CreateCommentRequest("Checked the logs.", true), _agent.Id, UserRole.Agent, CancellationToken.None);

        Assert.Equal(_agent.Id, Assert.Single(_comments.Comments).AuthorId);
        Assert.Equal(_agent.Id, response.AuthorId);
        Assert.Equal("Eva Agent", response.AuthorName);
    }

    [Fact]
    public async Task Creation_time_is_truncated_to_the_microsecond_the_database_keeps()
    {
        var ticket = SeedTicket();
        _time.Now = new DateTimeOffset(2026, 10, 7, 9, 30, 0, TimeSpan.Zero).AddTicks(1_234_567);

        var response = await _commentService.AddAsync(
            ticket.Id, new CreateCommentRequest("Any update?", false), _client.Id, UserRole.Client, CancellationToken.None);

        var expected = new DateTimeOffset(2026, 10, 7, 9, 30, 0, TimeSpan.Zero).AddTicks(1_234_560);
        Assert.Equal(expected, response.CreatedAt);
        Assert.Equal(expected, Assert.Single(_comments.Comments).CreatedAt);
    }

    [Fact]
    public async Task Content_is_trimmed()
    {
        var ticket = SeedTicket();

        var response = await _commentService.AddAsync(
            ticket.Id, new CreateCommentRequest("  Any update? ", false), _client.Id, UserRole.Client, CancellationToken.None);

        Assert.Equal("Any update?", response.Content);
    }

    // A valid token for a user that is no longer stored is an inconsistent server state, not a client error:
    // it fails with a clear exception (a generic 500 over HTTP) before anything is saved.
    [Fact]
    public async Task Author_missing_from_storage_fails_clearly_and_saves_nothing()
    {
        var ticket = SeedTicket();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _commentService.AddAsync(
            ticket.Id, new CreateCommentRequest("Any update?", false), Guid.NewGuid(), UserRole.Client, CancellationToken.None));

        Assert.Equal("The authenticated user does not exist.", exception.Message);
        AssertNothingSaved();
    }

    [Theory]
    [InlineData(UserRole.Agent)]
    [InlineData(UserRole.Admin)]
    public async Task Support_staff_write_internal_comments(UserRole role)
    {
        var ticket = SeedTicket();
        var authorId = role == UserRole.Agent ? _agent.Id : _admin.Id;

        var response = await _commentService.AddAsync(
            ticket.Id, new CreateCommentRequest("Checked the logs.", true), authorId, role, CancellationToken.None);

        Assert.True(response.IsInternal);
        Assert.True(Assert.Single(_comments.Comments).IsInternal);
    }

    [Fact]
    public async Task Commenting_leaves_the_ticket_unchanged()
    {
        var ticket = SeedTicket();

        await _commentService.AddAsync(
            ticket.Id, new CreateCommentRequest("Any update?", false), _client.Id, UserRole.Client, CancellationToken.None);

        Assert.Equal(TicketStatus.Open, ticket.Status);
        Assert.Null(ticket.AssignedToId);
    }

    [Theory]
    [InlineData(UserRole.Client, TicketVisibility.CreatedByUser)]
    [InlineData(UserRole.Agent, TicketVisibility.AssignedToUserOrUnassigned)]
    [InlineData(UserRole.Admin, TicketVisibility.All)]
    public async Task Adding_loads_the_ticket_within_the_callers_scope(UserRole role, TicketVisibility expected)
    {
        var ticket = SeedTicket();
        var authorId = UserFor(role).Id;

        await _commentService.AddAsync(
            ticket.Id, new CreateCommentRequest("Any update?", false), authorId, role, CancellationToken.None);

        Assert.Equal((expected, authorId), _tickets.LastVisibleLookup);
    }

    [Theory]
    [InlineData(UserRole.Client, TicketVisibility.CreatedByUser, CommentVisibility.PublicOnly)]
    [InlineData(UserRole.Agent, TicketVisibility.AssignedToUserOrUnassigned, CommentVisibility.IncludeInternal)]
    [InlineData(UserRole.Admin, TicketVisibility.All, CommentVisibility.IncludeInternal)]
    public async Task Listing_asks_for_the_ticket_and_comments_the_role_may_see(
        UserRole role,
        TicketVisibility ticketVisibility,
        CommentVisibility commentVisibility)
    {
        var ticket = SeedTicket();
        var userId = UserFor(role).Id;
        _comments.ListResult = [new CommentResponse(Guid.NewGuid(), ticket.Id, userId, "Name", "Text", false, _time.Now)];

        var comments = await _commentService.ListAsync(ticket.Id, userId, role, CancellationToken.None);

        Assert.Equal((ticketVisibility, userId), _tickets.LastVisibleLookup);
        Assert.Equal((ticket.Id, commentVisibility), _comments.LastListLookup);
        Assert.Same(_comments.ListResult, comments);
    }

    [Fact]
    public async Task Listing_comments_of_a_ticket_that_cannot_be_loaded_is_not_found()
    {
        await Assert.ThrowsAsync<TicketNotFoundException>(() =>
            _commentService.ListAsync(Guid.NewGuid(), _client.Id, UserRole.Client, CancellationToken.None));

        Assert.Null(_comments.LastListLookup);
    }

    [Fact]
    public async Task Unknown_role_fails_instead_of_widening_the_scope()
    {
        var ticket = SeedTicket();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _commentService.AddAsync(
            ticket.Id, new CreateCommentRequest("Any update?", true), _admin.Id, (UserRole)99, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _commentService.ListAsync(ticket.Id, _admin.Id, (UserRole)99, CancellationToken.None));

        Assert.Null(_comments.LastListLookup);
        AssertNothingSaved();
    }

    [Fact]
    public async Task Client_cannot_write_an_internal_comment_even_on_their_own_ticket()
    {
        var ticket = SeedTicket();

        var exception = await Assert.ThrowsAsync<TicketActionForbiddenException>(() => _commentService.AddAsync(
            ticket.Id, new CreateCommentRequest("Internal?", true), _client.Id, UserRole.Client, CancellationToken.None));

        Assert.Equal("Only support staff can write internal comments.", exception.Message);
        AssertNothingSaved();
    }

    [Fact]
    public async Task Adding_to_a_ticket_that_cannot_be_loaded_is_not_found()
    {
        await Assert.ThrowsAsync<TicketNotFoundException>(() => _commentService.AddAsync(
            Guid.NewGuid(), new CreateCommentRequest("Any update?", false), _client.Id, UserRole.Client, CancellationToken.None));

        AssertNothingSaved();
    }

    [Theory]
    [InlineData(UserRole.Client, false)]
    [InlineData(UserRole.Agent, true)]
    [InlineData(UserRole.Admin, true)]
    public async Task Commenting_on_a_closed_ticket_breaks_a_domain_rule_and_saves_nothing(UserRole role, bool isInternal)
    {
        var ticket = SeedTicket(closed: true);

        await Assert.ThrowsAsync<TicketRuleViolationException>(() => _commentService.AddAsync(
            ticket.Id, new CreateCommentRequest("Any update?", isInternal), UserFor(role).Id, role, CancellationToken.None));

        AssertNothingSaved();
    }

    // The ticket is closed and the caller a client asking for an internal comment, so passing proves
    // validation runs before the lookup, the role check and the domain rule.
    [Theory]
    [InlineData("", true)]
    [InlineData("   ", true)]
    [InlineData("Any update?", null)]
    public async Task Invalid_request_is_rejected_before_the_ticket_is_loaded(string content, bool? isInternal)
    {
        var ticket = SeedTicket(closed: true);

        await Assert.ThrowsAsync<ValidationException>(() => _commentService.AddAsync(
            ticket.Id, new CreateCommentRequest(content, isInternal), _client.Id, UserRole.Client, CancellationToken.None));

        Assert.Null(_tickets.LastVisibleLookup);
        AssertNothingSaved();
    }

    // A client asking for an internal comment gets 404, not 403, when the ticket cannot be loaded.
    [Fact]
    public async Task Missing_ticket_is_reported_before_the_role_check()
    {
        await Assert.ThrowsAsync<TicketNotFoundException>(() => _commentService.AddAsync(
            Guid.NewGuid(), new CreateCommentRequest("Internal?", true), _client.Id, UserRole.Client, CancellationToken.None));

        AssertNothingSaved();
    }

    // The ticket is closed, so passing proves the role is checked before the domain rule.
    [Fact]
    public async Task Role_check_runs_before_the_closed_ticket_rule()
    {
        var ticket = SeedTicket(closed: true);

        await Assert.ThrowsAsync<TicketActionForbiddenException>(() => _commentService.AddAsync(
            ticket.Id, new CreateCommentRequest("Internal?", true), _client.Id, UserRole.Client, CancellationToken.None));

        AssertNothingSaved();
    }

    private User SeedUser(string name, string email, UserRole role)
    {
        var user = new User(name, email, "hash", role);
        _users.Add(user);
        return user;
    }

    private User UserFor(UserRole role) => role switch
    {
        UserRole.Client => _client,
        UserRole.Agent => _agent,
        _ => _admin
    };

    // Closed through the domain, so every stored ticket is one the application could have produced.
    private Ticket SeedTicket(bool closed = false)
    {
        var ticket = new Ticket("Printer offline", "It shows error 42.", TicketPriority.High, Guid.NewGuid(), _client.Id, _time.Now);
        if (closed)
        {
            ticket.ChangeStatus(TicketStatus.Closed, _admin.Id, _time.Now);
        }

        _tickets.Add(ticket);
        return ticket;
    }

    private void AssertNothingSaved()
    {
        Assert.Empty(_comments.Comments);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }
}
