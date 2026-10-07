using Helpdesk.Application.Abstractions;
using Helpdesk.Application.Tickets;
using Helpdesk.Domain.Entities;
using Helpdesk.Domain.Enums;
using Helpdesk.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Helpdesk.Tests.Persistence;

[Collection(PostgreSqlCollection.Name)]
public class CommentRepositoryTests(PostgreSqlFixture fixture) : IAsyncLifetime
{
    private static readonly DateTimeOffset UtcNow = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    private string _connectionString = null!;
    private User _client = null!;
    private User _agent = null!;
    private Ticket _ticket = null!;

    public async Task InitializeAsync()
    {
        _connectionString = await fixture.CreateMigratedDatabaseAsync();
        _client = await SaveUserAsync("ana@example.com", UserRole.Client, "Ana Ruiz");
        _agent = await SaveUserAsync("luis@example.com", UserRole.Agent, "Luis Vega");
        _ticket = await SaveTicketAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Public_only_never_returns_internal_comments()
    {
        var publicComment = await SaveCommentAsync(_ticket, _client.Id, "Any update?", isInternal: false);
        await SaveCommentAsync(_ticket, _agent.Id, "Customer is on the legacy plan.", isInternal: true);

        var comments = await ListAsync(_ticket.Id, CommentVisibility.PublicOnly);

        Assert.Equal(publicComment.Id, Assert.Single(comments).Id);
        Assert.DoesNotContain(comments, comment => comment.IsInternal);
    }

    [Fact]
    public async Task Include_internal_returns_public_and_internal_comments()
    {
        var publicComment = await SaveCommentAsync(_ticket, _client.Id, "Any update?", isInternal: false);
        var internalComment = await SaveCommentAsync(_ticket, _agent.Id, "Checked the logs.", isInternal: true,
            createdAt: UtcNow.AddMinutes(1));

        var comments = await ListAsync(_ticket.Id, CommentVisibility.IncludeInternal);

        Assert.Equal([publicComment.Id, internalComment.Id], comments.Select(comment => comment.Id));
    }

    [Fact]
    public async Task Only_the_requested_tickets_comments_are_returned()
    {
        var otherTicket = await SaveTicketAsync();
        var mine = await SaveCommentAsync(_ticket, _client.Id, "Any update?", isInternal: false);
        await SaveCommentAsync(otherTicket, _client.Id, "Other ticket.", isInternal: false);

        var comments = await ListAsync(_ticket.Id, CommentVisibility.IncludeInternal);

        Assert.Equal(mine.Id, Assert.Single(comments).Id);
    }

    [Fact]
    public async Task Ticket_without_comments_returns_an_empty_list()
    {
        Assert.Empty(await ListAsync(_ticket.Id, CommentVisibility.IncludeInternal));
    }

    [Fact]
    public async Task Comments_are_ordered_oldest_first_with_the_id_breaking_ties()
    {
        var newest = await SaveCommentAsync(_ticket, _client.Id, "Third", isInternal: false, createdAt: UtcNow.AddHours(2));
        var tiedA = await SaveCommentAsync(_ticket, _client.Id, "Tied A", isInternal: false, createdAt: UtcNow.AddHours(1));
        var tiedB = await SaveCommentAsync(_ticket, _agent.Id, "Tied B", isInternal: true, createdAt: UtcNow.AddHours(1));
        var oldest = await SaveCommentAsync(_ticket, _agent.Id, "First", isInternal: false, createdAt: UtcNow);

        var comments = await ListAsync(_ticket.Id, CommentVisibility.IncludeInternal);

        // PostgreSQL orders uuids by their bytes, which is the ordinal order of their text form.
        var tied = new[] { tiedA.Id, tiedB.Id }.OrderBy(id => id.ToString(), StringComparer.Ordinal);
        Assert.Equal([oldest.Id, .. tied, newest.Id], comments.Select(comment => comment.Id));
    }

    [Fact]
    public async Task Comments_carry_the_authors_name_and_track_nothing()
    {
        var comment = await SaveCommentAsync(_ticket, _agent.Id, "Checked the logs.", isInternal: true);
        var services = TestConfiguration.CreateInfrastructureServices(_connectionString);

        var comments = await services.GetRequiredService<ICommentRepository>()
            .ListAsync(_ticket.Id, CommentVisibility.IncludeInternal, CancellationToken.None);

        Assert.Equal(
            new CommentResponse(comment.Id, _ticket.Id, _agent.Id, "Luis Vega", "Checked the logs.", true, UtcNow),
            Assert.Single(comments));
        Assert.Empty(services.GetRequiredService<HelpdeskDbContext>().ChangeTracker.Entries());
    }

    [Fact]
    public async Task Added_comment_is_saved_with_the_unit_of_work()
    {
        var services = TestConfiguration.CreateInfrastructureServices(_connectionString);
        var comment = _ticket.CreateComment(_client.Id, "Any update?", isInternal: false, UtcNow);

        services.GetRequiredService<ICommentRepository>().Add(comment);
        await services.GetRequiredService<IUnitOfWork>().SaveChangesAsync(CancellationToken.None);

        await using var context = PostgreSqlFixture.CreateContext(_connectionString);
        var stored = await context.Comments.SingleAsync();
        Assert.Equal(comment.Id, stored.Id);
        Assert.Equal(_ticket.Id, stored.TicketId);
        Assert.Equal(_client.Id, stored.AuthorId);
        Assert.Equal("Any update?", stored.Content);
        Assert.False(stored.IsInternal);
        Assert.Equal(UtcNow, stored.CreatedAt);
    }

    private async Task<IReadOnlyList<CommentResponse>> ListAsync(Guid ticketId, CommentVisibility visibility) =>
        await TestConfiguration.CreateInfrastructureServices(_connectionString)
            .GetRequiredService<ICommentRepository>()
            .ListAsync(ticketId, visibility, CancellationToken.None);

    private async Task<User> SaveUserAsync(string email, UserRole role, string name)
    {
        await using var context = PostgreSqlFixture.CreateContext(_connectionString);
        var user = new User(name, email, "hash", role);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private async Task<Ticket> SaveTicketAsync()
    {
        await using var context = PostgreSqlFixture.CreateContext(_connectionString);
        var categoryId = await context.Categories.Where(c => c.Name == "General").Select(c => c.Id).SingleAsync();
        var ticket = new Ticket("Printer offline", "It shows error 42.", TicketPriority.Medium, categoryId, _client.Id, UtcNow);
        context.Tickets.Add(ticket);
        await context.SaveChangesAsync();
        return ticket;
    }

    private async Task<Comment> SaveCommentAsync(
        Ticket ticket,
        Guid authorId,
        string content,
        bool isInternal,
        DateTimeOffset? createdAt = null)
    {
        await using var context = PostgreSqlFixture.CreateContext(_connectionString);
        var comment = ticket.CreateComment(authorId, content, isInternal, createdAt ?? UtcNow);
        context.Comments.Add(comment);
        await context.SaveChangesAsync();
        return comment;
    }
}
