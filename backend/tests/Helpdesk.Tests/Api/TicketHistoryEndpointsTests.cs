using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Helpdesk.Application.Abstractions;
using Helpdesk.Application.Tickets;
using Helpdesk.Domain.Entities;
using Helpdesk.Domain.Enums;
using Helpdesk.Tests.Application;
using Helpdesk.Tests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Helpdesk.Tests.Api.ProblemAssertions;

namespace Helpdesk.Tests.Api;

/// <summary>
/// Covers reading a ticket's audit history over HTTP: admins only, the 404 for a missing ticket, and a real
/// history built through the API, in order, with names and without emails.
/// </summary>
[Collection(PostgreSqlCollection.Name)]
public class TicketHistoryEndpointsTests(PostgreSqlFixture fixture) : IAsyncLifetime
{
    // Whole seconds, so the times read back from PostgreSQL, which keeps microseconds, compare exactly (#26).
    // Close to the real time, so the access tokens issued with the real clock stay valid whichever clock checks them.
    private static readonly DateTimeOffset Start = WholeSeconds(DateTimeOffset.UtcNow);

    private readonly FixedTimeProvider _time = new(Start);
    private string _connectionString = null!;
    private HelpdeskApiFactory _factory = null!;
    private HttpClient _client = null!;
    private Guid _categoryId;
    private User _owner = null!;
    private User _agent = null!;
    private User _admin = null!;

    public async Task InitializeAsync()
    {
        _connectionString = await fixture.CreateMigratedDatabaseAsync();
        _factory = new HelpdeskApiFactory(
            _connectionString,
            configureServices: services => services.AddSingleton<TimeProvider>(_time));
        _client = _factory.CreateClient();

        await using var context = PostgreSqlFixture.CreateContext(_connectionString);
        _categoryId = await context.Categories.Where(c => c.Name == "General").Select(c => c.Id).SingleAsync();
        _owner = await SaveUserAsync("Ana Ruiz", "ana@example.com", UserRole.Client);
        _agent = await SaveUserAsync("Eva Agent", "eva@example.com", UserRole.Agent);
        _admin = await SaveUserAsync("Admin User", "admin@example.com", UserRole.Admin);
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task History_of_a_ticket_created_assigned_and_started_through_the_api_is_in_order()
    {
        var created = await SendAsync(_owner, HttpMethod.Post, "/api/tickets", new
        {
            title = "Printer offline",
            description = "It shows error 42.",
            categoryId = _categoryId,
            priority = "High"
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var ticketId = (await created.Content.ReadFromJsonAsync<TicketResponse>())!.Id;

        _time.Now = Start.AddMinutes(1);
        var assigned = await SendAsync(_admin, HttpMethod.Put, $"/api/tickets/{ticketId}/assignee", new { assigneeId = _agent.Id });
        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);

        _time.Now = Start.AddMinutes(2);
        var started = await SendAsync(_agent, HttpMethod.Put, $"/api/tickets/{ticketId}/status", new { status = "InProgress" });
        Assert.Equal(HttpStatusCode.OK, started.StatusCode);

        var response = await GetHistoryAsync(_admin, ticketId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var history = (await response.Content.ReadFromJsonAsync<List<TicketHistoryEntryResponse>>())!;
        var storedIds = await StoredEntryIdsAsync(ticketId);
        Assert.Equal(
            [
                new TicketHistoryEntryResponse(
                    storedIds[0], "Status", null, "Open", null, null, _owner.Id, "Ana Ruiz", Start),
                new TicketHistoryEntryResponse(
                    storedIds[1], "AssignedToId", null, _agent.Id.ToString(), null, "Eva Agent",
                    _admin.Id, "Admin User", Start.AddMinutes(1)),
                new TicketHistoryEntryResponse(
                    storedIds[2], "Status", "Open", "InProgress", null, null,
                    _agent.Id, "Eva Agent", Start.AddMinutes(2))
            ],
            history);
    }

    [Fact]
    public async Task History_names_its_fields_in_camel_case_and_carries_no_email()
    {
        var ticket = await SaveTicketWithHistoryAsync();

        var response = await GetHistoryAsync(_admin, ticket.Id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var entry = JsonDocument.Parse(json).RootElement.EnumerateArray().First();
        Assert.Equal(
            ["id", "field", "oldValue", "newValue", "oldValueName", "newValueName", "changedById", "changedByName", "changedAt"],
            entry.EnumerateObject().Select(property => property.Name));
        Assert.DoesNotContain("@", json);
        foreach (var user in new[] { _owner, _agent, _admin })
        {
            Assert.DoesNotContain(user.Email, json, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [InlineData(UserRole.Client)]
    [InlineData(UserRole.Agent)]
    public async Task Non_admin_is_forbidden_even_on_a_ticket_they_own_or_work_on(UserRole role)
    {
        // The client owns the ticket and the agent is assigned to it, so the 403 comes from the role, not the scope.
        var ticket = await SaveTicketWithHistoryAsync();
        var caller = role == UserRole.Client ? _owner : _agent;

        var response = await GetHistoryAsync(caller, ticket.Id);

        await AssertProblemAsync(response, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Anonymous_request_is_rejected()
    {
        var ticket = await SaveTicketWithHistoryAsync();

        var response = await _client.GetAsync($"/api/tickets/{ticket.Id}/history");

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Unknown_ticket_is_not_found_for_an_admin()
    {
        var response = await GetHistoryAsync(_admin, Guid.NewGuid());

        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    private Task<HttpResponseMessage> GetHistoryAsync(User caller, Guid ticketId) =>
        SendAsync(caller, HttpMethod.Get, $"/api/tickets/{ticketId}/history");

    private Task<HttpResponseMessage> SendAsync(User caller, HttpMethod method, string uri, object? body = null)
    {
        var request = new HttpRequestMessage(method, uri);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TokenFor(caller));
        return _client.SendAsync(request);
    }

    private string TokenFor(User user) =>
        _factory.Services.GetRequiredService<IAccessTokenIssuer>().Issue(user, DateTimeOffset.UtcNow).Value;

    private async Task<List<Guid>> StoredEntryIdsAsync(Guid ticketId)
    {
        await using var context = PostgreSqlFixture.CreateContext(_connectionString);
        return await context.AuditLogs
            .Where(entry => entry.TicketId == ticketId)
            .OrderBy(entry => entry.ChangedAt)
            .Select(entry => entry.Id)
            .ToListAsync();
    }

    private async Task<User> SaveUserAsync(string name, string email, UserRole role)
    {
        await using var context = PostgreSqlFixture.CreateContext(_connectionString);
        var user = new User(name, email, "hash", role);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    // Built through the domain with its audit entries stored, so a caller who got through would see a real history.
    private async Task<Ticket> SaveTicketWithHistoryAsync()
    {
        await using var context = PostgreSqlFixture.CreateContext(_connectionString);
        var ticket = new Ticket("Printer offline", "It shows error 42.", TicketPriority.Medium, _categoryId, _owner.Id, Start);
        context.Tickets.Add(ticket);
        context.AuditLogs.Add(AuditLog.TicketCreated(ticket));
        context.AuditLogs.Add(ticket.Assign(_agent.Id, _admin.Id, Start.AddMinutes(1))!);
        await context.SaveChangesAsync();
        return ticket;
    }

    private static DateTimeOffset WholeSeconds(DateTimeOffset time) =>
        new(time.Ticks - time.Ticks % TimeSpan.TicksPerSecond, TimeSpan.Zero);
}
