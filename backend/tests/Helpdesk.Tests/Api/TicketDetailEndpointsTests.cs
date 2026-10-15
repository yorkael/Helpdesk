using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Helpdesk.Application.Abstractions;
using Helpdesk.Application.Tickets;
using Helpdesk.Domain.Entities;
using Helpdesk.Domain.Enums;
using Helpdesk.Infrastructure.Authentication;
using Helpdesk.Tests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using static Helpdesk.Tests.Api.ProblemAssertions;

namespace Helpdesk.Tests.Api;

/// <summary>
/// Covers the HTTP side of reading one ticket: the role from the token, the 404 for a ticket outside the caller's
/// scope, and the route. Which tickets each visibility returns is covered against the database in
/// TicketDetailQueryTests.
/// </summary>
[Collection(PostgreSqlCollection.Name)]
public class TicketDetailEndpointsTests(PostgreSqlFixture fixture) : IAsyncLifetime
{
    private static readonly DateTimeOffset UtcNow = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    private string _connectionString = null!;
    private HelpdeskApiFactory _factory = null!;
    private HttpClient _client = null!;
    private Guid _billingId;

    public async Task InitializeAsync()
    {
        _connectionString = await fixture.CreateMigratedDatabaseAsync();
        _factory = new HelpdeskApiFactory(_connectionString);
        _client = _factory.CreateClient();

        await using var context = PostgreSqlFixture.CreateContext(_connectionString);
        _billingId = await context.Categories.Where(c => c.Name == "Billing").Select(c => c.Id).SingleAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Client_reads_their_own_ticket_with_every_field()
    {
        var client = await SaveUserAsync("ana@example.com", UserRole.Client, "Ana Ruiz");
        var agent = await SaveUserAsync("luis@example.com", UserRole.Agent, "Luis Vega");
        var ticket = await SaveTicketAsync(client.Id, agent.Id, TicketStatus.InProgress);

        var detail = await ReadAsync(client, ticket.Id);

        Assert.Equal(
            new TicketDetailResponse(ticket.Id, "Invoice missing", "The March invoice never arrived.", "InProgress",
                "High", _billingId, "Billing", client.Id, "Ana Ruiz", agent.Id, "Luis Vega", UtcNow),
            detail);
    }

    [Fact]
    public async Task Response_uses_the_documented_json_shape()
    {
        var client = await SaveUserAsync("ana@example.com", UserRole.Client);
        var ticket = await SaveTicketAsync(client.Id);

        var response = await GetAsync(TokenFor(client), $"/api/tickets/{ticket.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(
            ["id", "title", "description", "status", "priority", "categoryId", "categoryName", "createdById",
                "createdByName", "assignedToId", "assignedToName", "createdAt"],
            body.EnumerateObject().Select(member => member.Name));
    }

    [Fact]
    public async Task Agent_reads_tickets_assigned_to_them_and_unassigned_ones()
    {
        var client = await SaveUserAsync("ana@example.com", UserRole.Client);
        var agent = await SaveUserAsync("luis@example.com", UserRole.Agent);
        var assigned = await SaveTicketAsync(client.Id, agent.Id);
        var unassigned = await SaveTicketAsync(client.Id);

        Assert.Equal(assigned.Id, (await ReadAsync(agent, assigned.Id)).Id);
        Assert.Equal(unassigned.Id, (await ReadAsync(agent, unassigned.Id)).Id);
    }

    [Fact]
    public async Task Admin_reads_any_ticket()
    {
        var client = await SaveUserAsync("ana@example.com", UserRole.Client);
        var agent = await SaveUserAsync("luis@example.com", UserRole.Agent);
        var admin = await SaveUserAsync("admin@example.com", UserRole.Admin);
        var ticket = await SaveTicketAsync(client.Id, agent.Id);

        Assert.Equal(ticket.Id, (await ReadAsync(admin, ticket.Id)).Id);
    }

    [Fact]
    public async Task Ticket_outside_the_callers_scope_gets_the_same_404_as_a_missing_one()
    {
        var owner = await SaveUserAsync("ana@example.com", UserRole.Client);
        var otherClient = await SaveUserAsync("luis@example.com", UserRole.Client);
        var agent = await SaveUserAsync("eva@example.com", UserRole.Agent);
        var otherAgent = await SaveUserAsync("mario@example.com", UserRole.Agent);
        var ticket = await SaveTicketAsync(owner.Id, otherAgent.Id);

        foreach (var caller in new[] { otherClient, agent })
        {
            var outOfScope = await NotFoundBodyAsync(await GetAsync(TokenFor(caller), $"/api/tickets/{ticket.Id}"));
            var missing = await NotFoundBodyAsync(await GetAsync(TokenFor(caller), $"/api/tickets/{Guid.NewGuid()}"));

            Assert.Equal(missing, outOfScope);
        }
    }

    // Comparing with another ticket endpoint, not only with a missing id on this one: a 404 built here instead of
    // by the exception handler would be the same for both ids and still differ from every other ticket endpoint.
    [Fact]
    public async Task Not_found_body_is_the_one_the_comments_endpoint_gives_for_the_same_ticket()
    {
        var owner = await SaveUserAsync("ana@example.com", UserRole.Client);
        var otherClient = await SaveUserAsync("luis@example.com", UserRole.Client);
        var ticket = await SaveTicketAsync(owner.Id);
        var token = TokenFor(otherClient);

        var detail = await NotFoundBodyAsync(await GetAsync(token, $"/api/tickets/{ticket.Id}"));
        var comments = await NotFoundBodyAsync(await GetAsync(token, $"/api/tickets/{ticket.Id}/comments"));

        Assert.Equal(comments, detail);
    }

    [Fact]
    public async Task Anonymous_request_is_rejected()
    {
        var client = await SaveUserAsync("ana@example.com", UserRole.Client);
        var ticket = await SaveTicketAsync(client.Id);

        var response = await _client.GetAsync($"/api/tickets/{ticket.Id}");

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Invalid_token_is_rejected()
    {
        var client = await SaveUserAsync("ana@example.com", UserRole.Client);
        var ticket = await SaveTicketAsync(client.Id);

        var response = await GetAsync("not-a-token", $"/api/tickets/{ticket.Id}");

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Validly_signed_token_with_an_invalid_role_reads_nothing()
    {
        var client = await SaveUserAsync("ana@example.com", UserRole.Client);
        var ticket = await SaveTicketAsync(client.Id);

        var response = await GetAsync(SignedTokenWithRole(client.Id, "0"), $"/api/tickets/{ticket.Id}");

        // A token the API issued always has a valid role, so this is a server fault, answered without details.
        var problem = await AssertProblemAsync(response, HttpStatusCode.InternalServerError);
        Assert.DoesNotContain("Invoice missing", problem.GetRawText());
        Assert.DoesNotContain("role", problem.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Malformed_id_with_a_token_matches_no_route()
    {
        var client = await SaveUserAsync("ana@example.com", UserRole.Client);

        var response = await GetAsync(TokenFor(client), "/api/tickets/not-a-guid");

        // The {id:guid} constraint leaves no endpoint, so no ticket is looked up: the status code pages give the
        // empty 404 a generic body, without the ticket's detail.
        var problem = await AssertProblemAsync(response, HttpStatusCode.NotFound);
        Assert.False(problem.TryGetProperty("detail", out _));
    }

    [Fact]
    public async Task Malformed_id_without_a_token_is_rejected()
    {
        var response = await _client.GetAsync("/api/tickets/not-a-guid");

        // The fallback policy requires a token even when no endpoint matches, so 401 comes before the 404.
        await AssertProblemAsync(response, HttpStatusCode.Unauthorized);
    }

    private async Task<TicketDetailResponse> ReadAsync(User user, Guid ticketId)
    {
        var response = await GetAsync(TokenFor(user), $"/api/tickets/{ticketId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TicketDetailResponse>())!;
    }

    // The whole problem body except traceId, which differs on every request.
    private static async Task<string> NotFoundBodyAsync(HttpResponseMessage response)
    {
        var problem = await AssertProblemAsync(response, HttpStatusCode.NotFound);
        var body = JsonNode.Parse(problem.GetRawText())!.AsObject();
        Assert.True(body.Remove("traceId"));
        return body.ToJsonString();
    }

    private Task<HttpResponseMessage> GetAsync(string accessToken, string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return _client.SendAsync(request);
    }

    private string TokenFor(User user) =>
        _factory.Services.GetRequiredService<IAccessTokenIssuer>().Issue(user, DateTimeOffset.UtcNow).Value;

    private string SignedTokenWithRole(Guid userId, string role)
    {
        var options = _factory.Services.GetRequiredService<JwtOptions>();
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = options.Issuer,
            Audience = options.Audience,
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(options.SigningKey, JwtOptions.SigningAlgorithm),
            Claims = new Dictionary<string, object>
            {
                [AccessTokenClaimTypes.Subject] = userId.ToString(),
                [AccessTokenClaimTypes.Role] = role
            }
        });
    }

    private async Task<User> SaveUserAsync(string email, UserRole role, string name = "Test User")
    {
        await using var context = PostgreSqlFixture.CreateContext(_connectionString);
        var user = new User(name, email, "hash", role);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private async Task<Ticket> SaveTicketAsync(
        Guid creatorId,
        Guid? assignedToId = null,
        TicketStatus status = TicketStatus.Open)
    {
        await using var context = PostgreSqlFixture.CreateContext(_connectionString);
        var ticket = new Ticket("Invoice missing", "The March invoice never arrived.", TicketPriority.High, _billingId,
            creatorId, UtcNow);
        context.Tickets.Add(ticket);

        // Set directly instead of through Assign and ChangeStatus, so a test can start from any status
        // without walking the transition table.
        context.Entry(ticket).Property(t => t.Status).CurrentValue = status;
        context.Entry(ticket).Property(t => t.AssignedToId).CurrentValue = assignedToId;

        await context.SaveChangesAsync();
        return ticket;
    }
}
