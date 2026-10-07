using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Helpdesk.Application.Abstractions;
using Helpdesk.Application.Tickets;
using Helpdesk.Domain.Entities;
using Helpdesk.Domain.Enums;
using Helpdesk.Infrastructure.Persistence;
using Helpdesk.Tests.Application;
using Helpdesk.Tests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Helpdesk.Tests.Api.ProblemAssertions;

namespace Helpdesk.Tests.Api;

/// <summary>
/// Covers assigning tickets and changing their status over HTTP: roles from the token, scope, validation,
/// domain rules, the audit rows written and concurrent changes.
/// </summary>
[Collection(PostgreSqlCollection.Name)]
public class TicketChangeEndpointsTests(PostgreSqlFixture fixture) : IAsyncLifetime
{
    private static readonly DateTimeOffset UtcNow = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    private string _connectionString = null!;
    private HelpdeskApiFactory _factory = null!;
    private HttpClient _client = null!;
    private Guid _categoryId;
    private User _owner = null!;
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
        _owner = await SaveUserAsync("ana@example.com", UserRole.Client);
        _agent = await SaveUserAsync("eva@example.com", UserRole.Agent);
        _otherAgent = await SaveUserAsync("mario@example.com", UserRole.Agent);
        _admin = await SaveUserAsync("admin@example.com", UserRole.Admin);
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Agent_takes_an_unassigned_ticket_and_the_change_is_audited()
    {
        var ticket = await SaveTicketAsync();

        var response = await PutAssigneeAsync(_agent, ticket.Id, new { assigneeId = _agent.Id });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<TicketResponse>())!;
        Assert.Equal(ticket.Id, body.Id);
        Assert.Equal(_agent.Id, body.AssignedToId);
        Assert.Equal("Open", body.Status);

        await using var context = PostgreSqlFixture.CreateContext(_connectionString);
        Assert.Equal(_agent.Id, (await context.Tickets.SingleAsync()).AssignedToId);
        var entry = await context.AuditLogs.SingleAsync();
        Assert.Equal(ticket.Id, entry.TicketId);
        Assert.Equal(_agent.Id, entry.UserId);
        Assert.Equal("AssignedToId", entry.Field);
        Assert.Null(entry.OldValue);
        Assert.Equal(_agent.Id.ToString(), entry.NewValue);
    }

    [Fact]
    public async Task Actor_in_the_audit_comes_from_the_token_not_from_the_body()
    {
        var ticket = await SaveTicketAsync(_agent.Id);

        var response = await PutStatusAsync(_agent, ticket.Id, new
        {
            status = "InProgress",
            userId = _admin.Id,
            actorId = _admin.Id,
            changedById = _admin.Id
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var context = PostgreSqlFixture.CreateContext(_connectionString);
        Assert.Equal(_agent.Id, (await context.AuditLogs.SingleAsync()).UserId);
    }

    [Fact]
    public async Task Admin_reassigns_a_ticket_and_the_audit_keeps_both_assignees()
    {
        var ticket = await SaveTicketAsync(_agent.Id);

        var response = await PutAssigneeAsync(_admin, ticket.Id, new { assigneeId = _otherAgent.Id });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var context = PostgreSqlFixture.CreateContext(_connectionString);
        Assert.Equal(_otherAgent.Id, (await context.Tickets.SingleAsync()).AssignedToId);
        var entry = await context.AuditLogs.SingleAsync();
        Assert.Equal(_admin.Id, entry.UserId);
        Assert.Equal(_agent.Id.ToString(), entry.OldValue);
        Assert.Equal(_otherAgent.Id.ToString(), entry.NewValue);
    }

    [Fact]
    public async Task Agent_resending_their_own_id_on_their_ticket_changes_nothing()
    {
        var ticket = await SaveTicketAsync(_agent.Id);

        var response = await PutAssigneeAsync(_agent, ticket.Id, new { assigneeId = _agent.Id });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(_agent.Id, (await response.Content.ReadFromJsonAsync<TicketResponse>())!.AssignedToId);
        await AssertNoAuditAsync();
    }

    [Fact]
    public async Task Agent_assigning_another_agent_is_forbidden()
    {
        var ticket = await SaveTicketAsync();

        var response = await PutAssigneeAsync(_agent, ticket.Id, new { assigneeId = _otherAgent.Id });

        var problem = await AssertProblemAsync(response, HttpStatusCode.Forbidden);
        Assert.Equal("Agents can only assign tickets to themselves.", problem.GetProperty("detail").GetString());
        await AssertTicketUnchangedAsync(assignedToId: null, TicketStatus.Open);
    }

    [Fact]
    public async Task Agent_acting_on_another_agents_ticket_gets_the_same_404_as_for_a_missing_ticket()
    {
        var ticket = await SaveTicketAsync(_otherAgent.Id);

        var takeOthers = await PutAssigneeAsync(_agent, ticket.Id, new { assigneeId = _agent.Id });
        var moveOthers = await PutStatusAsync(_agent, ticket.Id, new { status = "InProgress" });
        var takeMissing = await PutAssigneeAsync(_agent, Guid.NewGuid(), new { assigneeId = _agent.Id });

        var missingProblem = await AssertProblemAsync(takeMissing, HttpStatusCode.NotFound);
        foreach (var response in new[] { takeOthers, moveOthers })
        {
            var problem = await AssertProblemAsync(response, HttpStatusCode.NotFound);
            Assert.Equal(missingProblem.GetProperty("detail").GetString(), problem.GetProperty("detail").GetString());
            Assert.Equal(missingProblem.GetProperty("title").GetString(), problem.GetProperty("title").GetString());
        }

        await AssertTicketUnchangedAsync(_otherAgent.Id, TicketStatus.Open);
    }

    [Fact]
    public async Task Client_cannot_assign_or_change_status_even_on_their_own_ticket()
    {
        var ticket = await SaveTicketAsync();

        var assign = await PutAssigneeAsync(_owner, ticket.Id, new { assigneeId = _agent.Id });
        var status = await PutStatusAsync(_owner, ticket.Id, new { status = "Closed" });

        await AssertProblemAsync(assign, HttpStatusCode.Forbidden);
        await AssertProblemAsync(status, HttpStatusCode.Forbidden);
        await AssertTicketUnchangedAsync(assignedToId: null, TicketStatus.Open);
    }

    [Theory]
    [InlineData("assignee")]
    [InlineData("status")]
    public async Task Anonymous_request_is_rejected(string resource)
    {
        var ticket = await SaveTicketAsync();

        var response = await _client.PutAsJsonAsync($"/api/tickets/{ticket.Id}/{resource}", new { });

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Empty_body_is_a_validation_error_for_assigneeId()
    {
        var ticket = await SaveTicketAsync();

        var response = await PutAssigneeAsync(_admin, ticket.Id, new { });

        await AssertValidationErrorForAsync(response, "assigneeId");
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("inactive agent")]
    [InlineData("client")]
    [InlineData("admin")]
    public async Task Assignee_that_is_not_an_active_agent_is_a_validation_error(string assignee)
    {
        var ticket = await SaveTicketAsync();
        var assigneeId = assignee switch
        {
            "unknown" => Guid.NewGuid(),
            "inactive agent" => (await SaveUserAsync("old@example.com", UserRole.Agent, isActive: false)).Id,
            "client" => _owner.Id,
            _ => _admin.Id
        };

        var response = await PutAssigneeAsync(_admin, ticket.Id, new { assigneeId });

        var errors = await AssertValidationErrorForAsync(response, "assigneeId");
        Assert.Equal(["The assignee must be an active agent."],
            errors.GetProperty("assigneeId").EnumerateArray().Select(message => message.GetString()));
        await AssertTicketUnchangedAsync(assignedToId: null, TicketStatus.Open);
    }

    [Fact]
    public async Task Assigning_a_closed_ticket_is_a_conflict()
    {
        var ticket = await SaveTicketAsync(status: TicketStatus.Closed);

        var response = await PutAssigneeAsync(_admin, ticket.Id, new { assigneeId = _agent.Id });

        var problem = await AssertProblemAsync(response, HttpStatusCode.Conflict);
        Assert.Equal("A closed ticket cannot be assigned.", problem.GetProperty("detail").GetString());
        await AssertTicketUnchangedAsync(assignedToId: null, TicketStatus.Closed);
    }

    [Fact]
    public async Task Agent_moves_their_ticket_to_in_progress_and_the_change_is_audited()
    {
        var ticket = await SaveTicketAsync(_agent.Id);

        var response = await PutStatusAsync(_agent, ticket.Id, new { status = "InProgress" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("InProgress", (await response.Content.ReadFromJsonAsync<TicketResponse>())!.Status);
        await using var context = PostgreSqlFixture.CreateContext(_connectionString);
        Assert.Equal(TicketStatus.InProgress, (await context.Tickets.SingleAsync()).Status);
        var entry = await context.AuditLogs.SingleAsync();
        Assert.Equal(_agent.Id, entry.UserId);
        Assert.Equal("Status", entry.Field);
        Assert.Equal("Open", entry.OldValue);
        Assert.Equal("InProgress", entry.NewValue);
    }

    [Fact]
    public async Task Admin_changes_the_status_of_a_ticket_assigned_to_an_agent()
    {
        var ticket = await SaveTicketAsync(_agent.Id);

        var response = await PutStatusAsync(_admin, ticket.Id, new { status = "Closed" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var context = PostgreSqlFixture.CreateContext(_connectionString);
        Assert.Equal(_admin.Id, (await context.AuditLogs.SingleAsync()).UserId);
    }

    [Fact]
    public async Task Disallowed_transition_is_a_conflict_that_names_both_statuses()
    {
        var ticket = await SaveTicketAsync(_agent.Id);

        var response = await PutStatusAsync(_agent, ticket.Id, new { status = "Resolved" });

        var problem = await AssertProblemAsync(response, HttpStatusCode.Conflict);
        Assert.Equal("Cannot change status from Open to Resolved.", problem.GetProperty("detail").GetString());
        await AssertTicketUnchangedAsync(_agent.Id, TicketStatus.Open);
    }

    [Fact]
    public async Task Unassigned_ticket_cannot_be_moved_to_in_progress()
    {
        var ticket = await SaveTicketAsync();

        var response = await PutStatusAsync(_admin, ticket.Id, new { status = "InProgress" });

        var problem = await AssertProblemAsync(response, HttpStatusCode.Conflict);
        Assert.Equal("Assign the ticket before moving it to InProgress.", problem.GetProperty("detail").GetString());
        await AssertTicketUnchangedAsync(assignedToId: null, TicketStatus.Open);
    }

    [Fact]
    public async Task Changing_to_the_current_status_changes_nothing()
    {
        var ticket = await SaveTicketAsync(_agent.Id);

        var response = await PutStatusAsync(_agent, ticket.Id, new { status = "Open" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await AssertNoAuditAsync();
    }

    [Theory]
    [InlineData("""{}""")]
    [InlineData("""{"status":""}""")]
    [InlineData("""{"status":"inprogress"}""")]
    [InlineData("""{"status":"1"}""")]
    [InlineData("""{"status":1}""")]
    public async Task Invalid_status_is_a_validation_error_named_after_it(string json)
    {
        var ticket = await SaveTicketAsync(_agent.Id);
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/tickets/{ticket.Id}/status")
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TokenFor(_agent));

        var response = await _client.SendAsync(request);

        var problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.Contains(problem.GetProperty("errors").EnumerateObject(), error => error.Name.EndsWith("status"));
        Assert.DoesNotContain("Helpdesk", problem.GetRawText());
        await AssertNoAuditAsync();
    }

    [Fact]
    public async Task Two_agents_taking_the_same_ticket_leave_it_with_the_first_and_one_audit_row()
    {
        var ticket = await SaveTicketAsync();
        HttpResponseMessage? firstResponse = null;
        HttpClient client = null!;

        // The second agent's request is paused just before its save; the first agent's request runs and
        // completes in that gap, so the second one writes over a row that changed after it was read.
        var firstAgentTakesTheTicket = new BeforeFirstSaveInterceptor(async () =>
            firstResponse = await SendAsync(client, HttpMethod.Put, $"/api/tickets/{ticket.Id}/assignee",
                TokenFor(_agent), new { assigneeId = _agent.Id }));
        await using var factory = new HelpdeskApiFactory(_connectionString, configureServices: services =>
            services.ConfigureDbContext<HelpdeskDbContext>(options => options.AddInterceptors(firstAgentTakesTheTicket)));
        client = factory.CreateClient();

        var secondResponse = await SendAsync(client, HttpMethod.Put, $"/api/tickets/{ticket.Id}/assignee",
            TokenFor(_otherAgent), new { assigneeId = _otherAgent.Id });

        Assert.Equal(HttpStatusCode.OK, firstResponse?.StatusCode);
        var problem = await AssertProblemAsync(secondResponse, HttpStatusCode.Conflict);
        Assert.Equal("The ticket was changed by another request. Reload it and try again.",
            problem.GetProperty("detail").GetString());

        await using var context = PostgreSqlFixture.CreateContext(_connectionString);
        Assert.Equal(_agent.Id, (await context.Tickets.SingleAsync()).AssignedToId);
        var entry = await context.AuditLogs.SingleAsync();
        Assert.Equal(_agent.Id, entry.UserId);
        Assert.Equal(_agent.Id.ToString(), entry.NewValue);
    }

    private Task<HttpResponseMessage> PutAssigneeAsync(User caller, Guid ticketId, object body) =>
        SendAsync(_client, HttpMethod.Put, $"/api/tickets/{ticketId}/assignee", TokenFor(caller), body);

    private Task<HttpResponseMessage> PutStatusAsync(User caller, Guid ticketId, object body) =>
        SendAsync(_client, HttpMethod.Put, $"/api/tickets/{ticketId}/status", TokenFor(caller), body);

    private static Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        HttpMethod method,
        string uri,
        string accessToken,
        object body)
    {
        var request = new HttpRequestMessage(method, uri) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client.SendAsync(request);
    }

    private string TokenFor(User user) =>
        _factory.Services.GetRequiredService<IAccessTokenIssuer>().Issue(user, DateTimeOffset.UtcNow).Value;

    private static async Task<JsonElement> AssertValidationErrorForAsync(HttpResponseMessage response, string field)
    {
        var errors = (await AssertProblemAsync(response, HttpStatusCode.BadRequest)).GetProperty("errors");
        Assert.Equal([field], errors.EnumerateObject().Select(error => error.Name));
        return errors;
    }

    private async Task AssertNoAuditAsync()
    {
        await using var context = PostgreSqlFixture.CreateContext(_connectionString);
        Assert.Equal(0, await context.AuditLogs.CountAsync());
    }

    private async Task AssertTicketUnchangedAsync(Guid? assignedToId, TicketStatus status)
    {
        await using var context = PostgreSqlFixture.CreateContext(_connectionString);
        var stored = await context.Tickets.SingleAsync();
        Assert.Equal(assignedToId, stored.AssignedToId);
        Assert.Equal(status, stored.Status);
        Assert.Equal(0, await context.AuditLogs.CountAsync());
    }

    private async Task<User> SaveUserAsync(string email, UserRole role, bool isActive = true)
    {
        await using var context = PostgreSqlFixture.CreateContext(_connectionString);
        var user = new User("Test User", email, "hash", role);
        if (!isActive)
        {
            TestUsers.Deactivate(user);
        }

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
}
