using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Helpdesk.Application.Abstractions;
using Helpdesk.Application.Tickets;
using Helpdesk.Domain.Entities;
using Helpdesk.Domain.Enums;
using Helpdesk.Tests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Helpdesk.Tests.Api.ProblemAssertions;

namespace Helpdesk.Tests.Api;

/// <summary>
/// Covers commenting on tickets over HTTP: who may read and write which comments, scope, validation,
/// the closed-ticket rule and that internal comments never reach a client.
/// </summary>
[Collection(PostgreSqlCollection.Name)]
public class TicketCommentEndpointsTests(PostgreSqlFixture fixture) : IAsyncLifetime
{
    private const string InternalText = "Customer is on the legacy plan; do not offer a refund.";

    private static readonly DateTimeOffset UtcNow = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    private string _connectionString = null!;
    private HelpdeskApiFactory _factory = null!;
    private HttpClient _client = null!;
    private Guid _categoryId;
    private User _owner = null!;
    private User _otherClient = null!;
    private User _agent = null!;
    private User _otherAgent = null!;
    private User _admin = null!;

    public async Task InitializeAsync()
    {
        _connectionString = await fixture.CreateMigratedDatabaseAsync();
        _factory = new HelpdeskApiFactory(_connectionString);
        _client = _factory.CreateClient();

        await using var context = PostgreSqlFixture.CreateContext(_connectionString);
        _categoryId = await context.Categories.Where(c => c.Name == "General").Select(c => c.Id).SingleAsync();
        _owner = await SaveUserAsync("Ana Ruiz", "ana@example.com", UserRole.Client);
        _otherClient = await SaveUserAsync("Luis Vega", "luis@example.com", UserRole.Client);
        _agent = await SaveUserAsync("Eva Agent", "eva@example.com", UserRole.Agent);
        _otherAgent = await SaveUserAsync("Mario Agent", "mario@example.com", UserRole.Agent);
        _admin = await SaveUserAsync("Admin User", "admin@example.com", UserRole.Admin);
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Client_comments_on_their_ticket_without_changing_it()
    {
        var ticket = await SaveTicketAsync();

        var response = await PostCommentAsync(_owner, ticket.Id, new { content = "  Any update? ", isInternal = false });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Null(response.Headers.Location);
        var body = (await response.Content.ReadFromJsonAsync<CommentResponse>())!;
        Assert.Equal(ticket.Id, body.TicketId);
        Assert.Equal(_owner.Id, body.AuthorId);
        Assert.Equal("Ana Ruiz", body.AuthorName);
        Assert.Equal("Any update?", body.Content);
        Assert.False(body.IsInternal);

        await using var context = PostgreSqlFixture.CreateContext(_connectionString);
        var stored = await context.Comments.SingleAsync();
        Assert.Equal(body.Id, stored.Id);
        Assert.Equal(body.CreatedAt, stored.CreatedAt);
        var storedTicket = await context.Tickets.SingleAsync();
        Assert.Equal(TicketStatus.Open, storedTicket.Status);
        Assert.Null(storedTicket.AssignedToId);
        Assert.Equal(0, await context.AuditLogs.CountAsync());
    }

    [Fact]
    public async Task Agent_comments_internally_on_an_unassigned_ticket_and_admin_on_another_agents_ticket()
    {
        var unassigned = await SaveTicketAsync();
        var othersTicket = await SaveTicketAsync(_otherAgent.Id);

        var byAgent = await PostCommentAsync(_agent, unassigned.Id, new { content = "Checked the logs.", isInternal = true });
        var byAdmin = await PostCommentAsync(_admin, othersTicket.Id, new { content = "Escalated.", isInternal = true });

        Assert.Equal(HttpStatusCode.Created, byAgent.StatusCode);
        Assert.Equal(HttpStatusCode.Created, byAdmin.StatusCode);
        Assert.True((await byAgent.Content.ReadFromJsonAsync<CommentResponse>())!.IsInternal);
        Assert.True((await byAdmin.Content.ReadFromJsonAsync<CommentResponse>())!.IsInternal);
        await using var context = PostgreSqlFixture.CreateContext(_connectionString);
        Assert.Equal(2, await context.Comments.CountAsync(comment => comment.IsInternal));
    }

    [Fact]
    public async Task Author_comes_from_the_token_not_from_the_body()
    {
        var ticket = await SaveTicketAsync();

        var response = await PostCommentAsync(_owner, ticket.Id, new
        {
            content = "Any update?",
            isInternal = false,
            authorId = _admin.Id,
            userId = _admin.Id,
            authorName = "Admin User"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<CommentResponse>())!;
        Assert.Equal(_owner.Id, body.AuthorId);
        Assert.Equal("Ana Ruiz", body.AuthorName);
        await using var context = PostgreSqlFixture.CreateContext(_connectionString);
        Assert.Equal(_owner.Id, (await context.Comments.SingleAsync()).AuthorId);
    }

    [Fact]
    public async Task Created_and_listed_comment_carry_the_same_author_name_and_no_email()
    {
        var ticket = await SaveTicketAsync();

        var created = await PostCommentAsync(_owner, ticket.Id, new { content = "Any update?", isInternal = false });
        var listed = await GetCommentsAsync(_owner, ticket.Id);

        var createdJson = await created.Content.ReadAsStringAsync();
        var listedJson = await listed.Content.ReadAsStringAsync();
        var createdComment = JsonSerializer.Deserialize<CommentResponse>(createdJson, JsonSerializerOptions.Web)!;
        var listedComment = Assert.Single(JsonSerializer.Deserialize<CommentResponse[]>(listedJson, JsonSerializerOptions.Web)!);
        Assert.Equal(createdComment, listedComment);
        foreach (var json in new[] { createdJson, listedJson })
        {
            Assert.DoesNotContain("email", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(_owner.Email, json, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Client_never_sees_an_internal_comment_in_any_response()
    {
        var ticket = await SaveTicketAsync();
        var publicComment = await SaveCommentAsync(ticket, _owner.Id, "Any update?", isInternal: false);
        await SaveCommentAsync(ticket, _admin.Id, InternalText, isInternal: true);

        var commentsResponse = await GetCommentsAsync(_owner, ticket.Id);
        var ticketsResponse = await GetAsync(_owner, "/api/tickets");

        Assert.Equal(HttpStatusCode.OK, commentsResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ticketsResponse.StatusCode);
        var commentsJson = await commentsResponse.Content.ReadAsStringAsync();
        var ticketsJson = await ticketsResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain(InternalText, commentsJson);
        Assert.DoesNotContain(InternalText, ticketsJson);
        var comments = JsonSerializer.Deserialize<CommentResponse[]>(commentsJson, JsonSerializerOptions.Web)!;
        Assert.Equal(publicComment.Id, Assert.Single(comments).Id);
    }

    [Theory]
    [InlineData(UserRole.Agent)]
    [InlineData(UserRole.Admin)]
    public async Task Support_staff_see_public_and_internal_comments_oldest_first(UserRole role)
    {
        var ticket = await SaveTicketAsync();
        var publicComment = await SaveCommentAsync(ticket, _owner.Id, "Any update?", isInternal: false);
        var internalComment = await SaveCommentAsync(ticket, _admin.Id, InternalText, isInternal: true, UtcNow.AddMinutes(1));

        var response = await GetCommentsAsync(role == UserRole.Agent ? _agent : _admin, ticket.Id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var comments = (await response.Content.ReadFromJsonAsync<CommentResponse[]>())!;
        Assert.Equal([publicComment.Id, internalComment.Id], comments.Select(comment => comment.Id));
        Assert.Equal(["Ana Ruiz", "Admin User"], comments.Select(comment => comment.AuthorName));
    }

    [Fact]
    public async Task Ticket_without_comments_lists_an_empty_array()
    {
        var ticket = await SaveTicketAsync();

        var response = await GetCommentsAsync(_owner, ticket.Id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("[]", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Client_cannot_write_an_internal_comment_even_on_their_own_ticket()
    {
        var ticket = await SaveTicketAsync();

        var response = await PostCommentAsync(_owner, ticket.Id, new { content = "Note to staff.", isInternal = true });

        var problem = await AssertProblemAsync(response, HttpStatusCode.Forbidden);
        Assert.Equal("Only support staff can write internal comments.", problem.GetProperty("detail").GetString());
        await AssertNoCommentsAsync();
    }

    [Fact]
    public async Task Another_client_gets_the_same_404_as_for_a_missing_ticket()
    {
        var ticket = await SaveTicketAsync();

        var read = await GetCommentsAsync(_otherClient, ticket.Id);
        var write = await PostCommentAsync(_otherClient, ticket.Id, new { content = "Hello?", isInternal = false });
        var writeInternal = await PostCommentAsync(_otherClient, ticket.Id, new { content = "Hello?", isInternal = true });
        var missing = await GetCommentsAsync(_otherClient, Guid.NewGuid());

        await AssertSameNotFoundAsync(missing, read, write, writeInternal);
        await AssertNoCommentsAsync();
    }

    [Fact]
    public async Task Agent_gets_the_same_404_for_another_agents_ticket_as_for_a_missing_ticket()
    {
        var ticket = await SaveTicketAsync(_otherAgent.Id);

        var read = await GetCommentsAsync(_agent, ticket.Id);
        var write = await PostCommentAsync(_agent, ticket.Id, new { content = "Checked the logs.", isInternal = true });
        var missing = await PostCommentAsync(_agent, Guid.NewGuid(), new { content = "Checked the logs.", isInternal = true });

        await AssertSameNotFoundAsync(missing, read, write);
        await AssertNoCommentsAsync();
    }

    [Theory]
    [InlineData(UserRole.Client, false)]
    [InlineData(UserRole.Agent, true)]
    [InlineData(UserRole.Admin, false)]
    public async Task Commenting_on_a_closed_ticket_is_a_conflict(UserRole role, bool isInternal)
    {
        var ticket = await SaveTicketAsync(status: TicketStatus.Closed);
        var caller = role switch
        {
            UserRole.Client => _owner,
            UserRole.Agent => _agent,
            _ => _admin
        };

        var response = await PostCommentAsync(caller, ticket.Id, new { content = "Any update?", isInternal });

        var problem = await AssertProblemAsync(response, HttpStatusCode.Conflict);
        Assert.Equal("A closed ticket cannot receive comments.", problem.GetProperty("detail").GetString());
        await AssertNoCommentsAsync();
    }

    [Fact]
    public async Task Comments_of_a_closed_ticket_can_still_be_read()
    {
        var ticket = await SaveTicketAsync();
        await SaveCommentAsync(ticket, _owner.Id, "Any update?", isInternal: false);
        await CloseAsync(ticket.Id);

        var response = await GetCommentsAsync(_owner, ticket.Id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single((await response.Content.ReadFromJsonAsync<CommentResponse[]>())!);
    }

    [Theory]
    [InlineData("""{"isInternal":false}""")]
    [InlineData("""{"content":null,"isInternal":false}""")]
    [InlineData("""{"content":"","isInternal":false}""")]
    [InlineData("""{"content":"   ","isInternal":false}""")]
    public async Task Missing_or_blank_content_is_a_validation_error_for_content(string json)
    {
        var ticket = await SaveTicketAsync();

        var response = await PostRawAsync(_owner, ticket.Id, json);

        await AssertValidationErrorForAsync(response, "content");
        await AssertNoCommentsAsync();
    }

    [Fact]
    public async Task Content_longer_than_4000_characters_is_a_validation_error_for_content()
    {
        var ticket = await SaveTicketAsync();

        var tooLong = await PostCommentAsync(_owner, ticket.Id, new { content = new string('a', 4001), isInternal = false });
        var longest = await PostCommentAsync(_owner, ticket.Id, new { content = new string('a', 4000), isInternal = false });

        await AssertValidationErrorForAsync(tooLong, "content");
        Assert.Equal(HttpStatusCode.Created, longest.StatusCode);
    }

    // Required rather than defaulting to public, so a forgotten flag cannot show an internal note to the client.
    [Theory]
    [InlineData("""{"content":"Checked the logs."}""")]
    [InlineData("""{"content":"Checked the logs.","isInternal":null}""")]
    public async Task Missing_visibility_is_a_validation_error_for_isInternal(string json)
    {
        var ticket = await SaveTicketAsync();

        var response = await PostRawAsync(_agent, ticket.Id, json);

        await AssertValidationErrorForAsync(response, "isInternal");
        await AssertNoCommentsAsync();
    }

    // A value of the wrong type never reaches FluentValidation: it cannot be deserialized into the bool.
    [Theory]
    [InlineData("""{"content":"Checked the logs.","isInternal":"yes"}""")]
    [InlineData("""{"content":"Checked the logs.","isInternal":"true"}""")]
    [InlineData("""{"content":"Checked the logs.","isInternal":1}""")]
    public async Task Visibility_of_the_wrong_type_is_rejected_with_a_generic_message(string json)
    {
        var ticket = await SaveTicketAsync();

        var response = await PostRawAsync(_agent, ticket.Id, json);

        var problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        var error = Assert.Single(problem.GetProperty("errors").EnumerateObject());
        Assert.Equal("$.isInternal", error.Name);
        Assert.Equal("The input was not valid.", Assert.Single(error.Value.EnumerateArray()).GetString());
        Assert.DoesNotContain("Helpdesk", problem.GetRawText());
        await AssertNoCommentsAsync();
    }

    [Fact]
    public async Task Anonymous_requests_are_rejected()
    {
        var ticket = await SaveTicketAsync();

        var read = await _client.GetAsync($"/api/tickets/{ticket.Id}/comments");
        var write = await _client.PostAsJsonAsync($"/api/tickets/{ticket.Id}/comments", new { content = "Hi", isInternal = false });

        await AssertProblemAsync(read, HttpStatusCode.Unauthorized);
        await AssertProblemAsync(write, HttpStatusCode.Unauthorized);
        await AssertNoCommentsAsync();
    }

    private Task<HttpResponseMessage> PostCommentAsync(User caller, Guid ticketId, object body) =>
        SendAsync(HttpMethod.Post, $"/api/tickets/{ticketId}/comments", caller, JsonContent.Create(body));

    private Task<HttpResponseMessage> PostRawAsync(User caller, Guid ticketId, string json) =>
        SendAsync(HttpMethod.Post, $"/api/tickets/{ticketId}/comments", caller,
            new StringContent(json, Encoding.UTF8, "application/json"));

    private Task<HttpResponseMessage> GetCommentsAsync(User caller, Guid ticketId) =>
        GetAsync(caller, $"/api/tickets/{ticketId}/comments");

    private Task<HttpResponseMessage> GetAsync(User caller, string uri) => SendAsync(HttpMethod.Get, uri, caller, null);

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string uri, User caller, HttpContent? content)
    {
        var request = new HttpRequestMessage(method, uri) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TokenFor(caller));
        return _client.SendAsync(request);
    }

    private string TokenFor(User user) =>
        _factory.Services.GetRequiredService<IAccessTokenIssuer>().Issue(user, DateTimeOffset.UtcNow).Value;

    private static async Task AssertSameNotFoundAsync(HttpResponseMessage missing, params HttpResponseMessage[] responses)
    {
        var missingProblem = await AssertProblemAsync(missing, HttpStatusCode.NotFound);
        foreach (var response in responses)
        {
            var problem = await AssertProblemAsync(response, HttpStatusCode.NotFound);
            Assert.Equal(missingProblem.GetProperty("detail").GetString(), problem.GetProperty("detail").GetString());
            Assert.Equal(missingProblem.GetProperty("title").GetString(), problem.GetProperty("title").GetString());
        }
    }

    private static async Task AssertValidationErrorForAsync(HttpResponseMessage response, string field)
    {
        var errors = (await AssertProblemAsync(response, HttpStatusCode.BadRequest)).GetProperty("errors");
        Assert.Equal([field], errors.EnumerateObject().Select(error => error.Name));
    }

    private async Task AssertNoCommentsAsync()
    {
        await using var context = PostgreSqlFixture.CreateContext(_connectionString);
        Assert.Equal(0, await context.Comments.CountAsync());
    }

    private async Task<User> SaveUserAsync(string name, string email, UserRole role)
    {
        await using var context = PostgreSqlFixture.CreateContext(_connectionString);
        var user = new User(name, email, "hash", role);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    // Built through the domain methods without storing their audit entries, so every audit row a test
    // finds was written by the request under test.
    private async Task<Ticket> SaveTicketAsync(Guid? assigneeId = null, TicketStatus status = TicketStatus.Open)
    {
        await using var context = PostgreSqlFixture.CreateContext(_connectionString);
        var ticket = new Ticket("Printer offline", "It shows error 42.", TicketPriority.Medium, _categoryId, _owner.Id, UtcNow);
        if (assigneeId is { } id)
        {
            ticket.Assign(id, _admin.Id, UtcNow);
        }

        if (status != TicketStatus.Open)
        {
            ticket.ChangeStatus(status, _admin.Id, UtcNow);
        }

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

    private async Task CloseAsync(Guid ticketId)
    {
        await using var context = PostgreSqlFixture.CreateContext(_connectionString);
        var ticket = await context.Tickets.SingleAsync(t => t.Id == ticketId);
        ticket.ChangeStatus(TicketStatus.Closed, _admin.Id, UtcNow);
        await context.SaveChangesAsync();
    }
}
