using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Helpdesk.Application.Abstractions;
using Helpdesk.Application.Authentication;
using Helpdesk.Application.Tickets;
using Helpdesk.Domain.Entities;
using Helpdesk.Domain.Enums;
using Helpdesk.Tests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Helpdesk.Tests.Api.ProblemAssertions;

namespace Helpdesk.Tests.Api;

[Collection(PostgreSqlCollection.Name)]
public class TicketEndpointsTests(PostgreSqlFixture fixture) : IAsyncLifetime
{
    private const string Password = "correct horse battery";

    private string _connectionString = null!;
    private HelpdeskApiFactory _factory = null!;
    private HttpClient _client = null!;
    private Guid _categoryId;

    public async Task InitializeAsync()
    {
        _connectionString = await fixture.CreateMigratedDatabaseAsync();
        _factory = new HelpdeskApiFactory(_connectionString);
        _client = _factory.CreateClient();

        await using var context = PostgreSqlFixture.CreateContext(_connectionString);
        _categoryId = await context.Categories.Where(c => c.Name == "Technical issue").Select(c => c.Id).SingleAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Client_creates_an_open_ticket_owned_by_them_and_the_creation_is_audited()
    {
        var client = await RegisterAndLogInAsync("ana@example.com");

        var response = await PostTicketAsync(
            client.AccessToken,
            new CreateTicketRequest("Printer offline", "It shows error 42.", _categoryId, "High"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Null(response.Headers.Location);
        var ticket = (await response.Content.ReadFromJsonAsync<TicketResponse>())!;
        Assert.Equal("Printer offline", ticket.Title);
        Assert.Equal("Open", ticket.Status);
        Assert.Equal("High", ticket.Priority);
        Assert.Equal(_categoryId, ticket.CategoryId);
        Assert.Equal(client.UserId, ticket.CreatedById);
        Assert.Null(ticket.AssignedToId);

        await using var context = PostgreSqlFixture.CreateContext(_connectionString);
        var stored = await context.Tickets.SingleAsync();
        Assert.Equal(ticket.Id, stored.Id);
        Assert.Equal(client.UserId, stored.CreatedById);
        var entry = await context.AuditLogs.SingleAsync();
        Assert.Equal(ticket.Id, entry.TicketId);
        Assert.Equal(client.UserId, entry.UserId);
        Assert.Equal("Status", entry.Field);
        Assert.Null(entry.OldValue);
        Assert.Equal("Open", entry.NewValue);
    }

    [Fact]
    public async Task Assignee_and_status_sent_by_a_client_are_ignored()
    {
        var client = await RegisterAndLogInAsync("ana@example.com");
        var agent = await SaveUserAsync("luis@example.com", UserRole.Agent);

        var response = await PostTicketAsync(client.AccessToken, new
        {
            title = "Printer offline",
            description = "It shows error 42.",
            categoryId = _categoryId,
            priority = "High",
            assigneeId = agent.Id,
            assignedToId = agent.Id,
            status = "Closed"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var ticket = (await response.Content.ReadFromJsonAsync<TicketResponse>())!;
        Assert.Equal("Open", ticket.Status);
        Assert.Null(ticket.AssignedToId);

        await using var context = PostgreSqlFixture.CreateContext(_connectionString);
        var stored = await context.Tickets.SingleAsync();
        Assert.Equal(TicketStatus.Open, stored.Status);
        Assert.Null(stored.AssignedToId);
    }

    [Theory]
    [InlineData(UserRole.Agent)]
    [InlineData(UserRole.Admin)]
    public async Task Staff_cannot_create_tickets(UserRole role)
    {
        var accessToken = _factory.Services.GetRequiredService<IAccessTokenIssuer>()
            .Issue(new User("Staff Member", "staff@example.com", "hash", role), DateTimeOffset.UtcNow)
            .Value;

        var response = await PostTicketAsync(
            accessToken,
            new CreateTicketRequest("Printer offline", "It shows error 42.", _categoryId, "High"));

        await AssertProblemAsync(response, HttpStatusCode.Forbidden);
        await using var context = PostgreSqlFixture.CreateContext(_connectionString);
        Assert.Equal(0, await context.Tickets.CountAsync());
    }

    [Fact]
    public async Task Anonymous_request_is_rejected()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/tickets",
            new CreateTicketRequest("Printer offline", "It shows error 42.", _categoryId, "High"));

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("high")]
    [InlineData("Critical")]
    public async Task Priority_that_is_not_an_exact_name_is_a_validation_error(string priority)
    {
        var client = await RegisterAndLogInAsync("ana@example.com");

        var response = await PostTicketAsync(
            client.AccessToken,
            new CreateTicketRequest("Printer offline", "It shows error 42.", _categoryId, priority));

        var errors = (await AssertProblemAsync(response, HttpStatusCode.BadRequest)).GetProperty("errors");
        Assert.Equal(["priority"], errors.EnumerateObject().Select(error => error.Name));
    }

    [Fact]
    public async Task Numeric_priority_is_rejected_by_deserialization_without_exposing_internal_types()
    {
        var client = await RegisterAndLogInAsync("ana@example.com");

        var response = await PostTicketAsync(client.AccessToken, new
        {
            title = "Printer offline",
            description = "It shows error 42.",
            categoryId = _categoryId,
            priority = 1
        });

        // A number never reaches FluentValidation: it cannot be deserialized into the string property.
        var problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        var error = Assert.Single(problem.GetProperty("errors").EnumerateObject());
        Assert.Equal("$.priority", error.Name);
        Assert.Equal("The input was not valid.", Assert.Single(error.Value.EnumerateArray()).GetString());
        Assert.DoesNotContain("Helpdesk", problem.GetRawText());
    }

    [Fact]
    public async Task Unknown_category_is_a_validation_error()
    {
        var client = await RegisterAndLogInAsync("ana@example.com");

        var response = await PostTicketAsync(
            client.AccessToken,
            new CreateTicketRequest("Printer offline", "It shows error 42.", Guid.NewGuid(), "High"));

        var errors = (await AssertProblemAsync(response, HttpStatusCode.BadRequest)).GetProperty("errors");
        Assert.Equal(["categoryId"], errors.EnumerateObject().Select(error => error.Name));
        await using var context = PostgreSqlFixture.CreateContext(_connectionString);
        Assert.Equal(0, await context.Tickets.CountAsync());
        Assert.Equal(0, await context.AuditLogs.CountAsync());
    }

    [Fact]
    public async Task Empty_body_reports_each_field_once()
    {
        var client = await RegisterAndLogInAsync("ana@example.com");

        var response = await PostTicketAsync(client.AccessToken, new { });

        var errors = (await AssertProblemAsync(response, HttpStatusCode.BadRequest)).GetProperty("errors");
        Assert.Equal(
            ["categoryId", "description", "priority", "title"],
            errors.EnumerateObject().Select(error => error.Name).Order());
        Assert.All(errors.EnumerateObject(), error => Assert.Single(error.Value.EnumerateArray()));
    }

    private async Task<(Guid UserId, string AccessToken)> RegisterAndLogInAsync(string email)
    {
        var registered = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("Ana Ruiz", email, Password));
        var user = (await registered.Content.ReadFromJsonAsync<UserResponse>())!;

        var login = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, Password));
        var tokens = (await login.Content.ReadFromJsonAsync<AuthTokensResponse>())!;

        return (user.Id, tokens.AccessToken);
    }

    private async Task<User> SaveUserAsync(string email, UserRole role)
    {
        await using var context = PostgreSqlFixture.CreateContext(_connectionString);
        var user = new User("Luis Vega", email, "hash", role);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private Task<HttpResponseMessage> PostTicketAsync(string accessToken, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/tickets") { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return _client.SendAsync(request);
    }
}
