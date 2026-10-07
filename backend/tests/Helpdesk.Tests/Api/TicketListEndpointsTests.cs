using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
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
/// Covers the HTTP side of listing: the role from the token, parameter binding and errors.
/// Filter, search, ordering and paging semantics are covered against the database in TicketListQueryTests.
/// </summary>
[Collection(PostgreSqlCollection.Name)]
public class TicketListEndpointsTests(PostgreSqlFixture fixture) : IAsyncLifetime
{
    private static readonly DateTimeOffset UtcNow = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    private string _connectionString = null!;
    private HelpdeskApiFactory _factory = null!;
    private HttpClient _client = null!;
    private Guid _generalId;
    private Guid _billingId;

    public async Task InitializeAsync()
    {
        _connectionString = await fixture.CreateMigratedDatabaseAsync();
        _factory = new HelpdeskApiFactory(_connectionString);
        _client = _factory.CreateClient();

        await using var context = PostgreSqlFixture.CreateContext(_connectionString);
        _generalId = await context.Categories.Where(c => c.Name == "General").Select(c => c.Id).SingleAsync();
        _billingId = await context.Categories.Where(c => c.Name == "Billing").Select(c => c.Id).SingleAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Client_sees_only_their_own_tickets()
    {
        var ana = await SaveUserAsync("ana@example.com", UserRole.Client);
        var luis = await SaveUserAsync("luis@example.com", UserRole.Client);
        var first = await SaveTicketAsync(ana.Id);
        var second = await SaveTicketAsync(ana.Id);
        await SaveTicketAsync(luis.Id);

        var page = await ListAsync(ana);

        Assert.Equal(2, page.TotalCount);
        Assert.Equal(new[] { first.Id, second.Id }.Order(), page.Items.Select(item => item.Id).Order());
    }

    [Fact]
    public async Task Client_cannot_see_another_clients_tickets_through_a_createdById_parameter()
    {
        var ana = await SaveUserAsync("ana@example.com", UserRole.Client);
        var luis = await SaveUserAsync("luis@example.com", UserRole.Client);
        var own = await SaveTicketAsync(ana.Id);
        await SaveTicketAsync(luis.Id);

        var page = await ListAsync(ana, $"createdById={luis.Id}");

        Assert.Equal(own.Id, Assert.Single(page.Items).Id);
        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public async Task Agent_sees_tickets_assigned_to_them_or_unassigned()
    {
        var client = await SaveUserAsync("ana@example.com", UserRole.Client);
        var agent = await SaveUserAsync("luis@example.com", UserRole.Agent);
        var otherAgent = await SaveUserAsync("eva@example.com", UserRole.Agent);
        var assigned = await SaveTicketAsync(client.Id, assignedToId: agent.Id);
        var unassigned = await SaveTicketAsync(client.Id);
        await SaveTicketAsync(client.Id, assignedToId: otherAgent.Id);

        var page = await ListAsync(agent);

        Assert.Equal(2, page.TotalCount);
        Assert.Equal(new[] { assigned.Id, unassigned.Id }.Order(), page.Items.Select(item => item.Id).Order());
    }

    [Fact]
    public async Task Agent_filtering_by_another_agent_gets_an_empty_list()
    {
        var client = await SaveUserAsync("ana@example.com", UserRole.Client);
        var agent = await SaveUserAsync("luis@example.com", UserRole.Agent);
        var otherAgent = await SaveUserAsync("eva@example.com", UserRole.Agent);
        await SaveTicketAsync(client.Id, assignedToId: otherAgent.Id);
        await SaveTicketAsync(client.Id);

        var page = await ListAsync(agent, $"assignedToId={otherAgent.Id}");

        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
    }

    [Fact]
    public async Task Admin_sees_every_ticket()
    {
        var ana = await SaveUserAsync("ana@example.com", UserRole.Client);
        var luis = await SaveUserAsync("luis@example.com", UserRole.Client);
        var agent = await SaveUserAsync("eva@example.com", UserRole.Agent);
        var admin = await SaveUserAsync("admin@example.com", UserRole.Admin);
        await SaveTicketAsync(ana.Id);
        await SaveTicketAsync(luis.Id, assignedToId: agent.Id);

        var page = await ListAsync(admin);

        Assert.Equal(2, page.TotalCount);
        Assert.Equal(2, page.Items.Count);
    }

    [Fact]
    public async Task Every_query_parameter_is_bound_by_its_name()
    {
        var client = await SaveUserAsync("ana@example.com", UserRole.Client);
        var agent = await SaveUserAsync("luis@example.com", UserRole.Agent);
        var admin = await SaveUserAsync("admin@example.com", UserRole.Admin);
        var match = await SaveTicketAsync(client.Id, "Invoice missing", TicketPriority.High, _billingId,
            TicketStatus.InProgress, agent.Id);
        // Each decoy differs from the match in exactly one filter.
        await SaveTicketAsync(client.Id, "Invoice missing", TicketPriority.High, _billingId, TicketStatus.Open, agent.Id);
        await SaveTicketAsync(client.Id, "Invoice missing", TicketPriority.Low, _billingId, TicketStatus.InProgress, agent.Id);
        await SaveTicketAsync(client.Id, "Invoice missing", TicketPriority.High, _generalId, TicketStatus.InProgress, agent.Id);
        await SaveTicketAsync(client.Id, "Invoice missing", TicketPriority.High, _billingId, TicketStatus.InProgress);
        await SaveTicketAsync(client.Id, "Printer offline", TicketPriority.High, _billingId, TicketStatus.InProgress, agent.Id);

        var page = await ListAsync(
            admin,
            $"status=InProgress&priority=High&categoryId={_billingId}&assignedToId={agent.Id}&search=invoice&page=1&pageSize=5");

        Assert.Equal(match.Id, Assert.Single(page.Items).Id);
        Assert.Equal(1, page.TotalCount);
        Assert.Equal(1, page.Page);
        Assert.Equal(5, page.PageSize);
    }

    [Fact]
    public async Task Missing_paging_parameters_return_the_first_page_of_20()
    {
        var client = await SaveUserAsync("ana@example.com", UserRole.Client);
        await SaveTicketsAsync(client.Id, 21);

        var page = await ListAsync(client);

        Assert.Equal(20, page.Items.Count);
        Assert.Equal(1, page.Page);
        Assert.Equal(20, page.PageSize);
        Assert.Equal(21, page.TotalCount);
    }

    [Fact]
    public async Task Maximum_page_size_is_accepted()
    {
        var client = await SaveUserAsync("ana@example.com", UserRole.Client);
        await SaveTicketsAsync(client.Id, 21);

        var page = await ListAsync(client, "pageSize=100");

        Assert.Equal(21, page.Items.Count);
        Assert.Equal(100, page.PageSize);
    }

    [Fact]
    public async Task Page_past_the_end_is_empty_and_still_reports_the_total()
    {
        var client = await SaveUserAsync("ana@example.com", UserRole.Client);
        await SaveTicketsAsync(client.Id, 3);

        var page = await ListAsync(client, "page=3&pageSize=2");

        Assert.Empty(page.Items);
        Assert.Equal(3, page.Page);
        Assert.Equal(3, page.TotalCount);
    }

    [Fact]
    public async Task Response_uses_the_documented_json_shape()
    {
        var client = await SaveUserAsync("ana@example.com", UserRole.Client);
        await SaveTicketAsync(client.Id);

        var response = await GetTicketsAsync(TokenFor(client), "");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(["items", "page", "pageSize", "totalCount"], body.EnumerateObject().Select(member => member.Name));
        Assert.Equal(
            ["id", "title", "status", "priority", "categoryId", "categoryName", "createdById", "createdByName",
                "assignedToId", "assignedToName", "createdAt"],
            body.GetProperty("items")[0].EnumerateObject().Select(member => member.Name));
    }

    [Theory]
    [InlineData("page=0", "page")]
    [InlineData("page=2147483647&pageSize=100", "page")]
    [InlineData("pageSize=0", "pageSize")]
    [InlineData("pageSize=101", "pageSize")]
    [InlineData("status=Unknown", "status")]
    [InlineData("status=open", "status")]
    [InlineData("status=1", "status")]
    [InlineData("priority=high", "priority")]
    public async Task Invalid_parameter_is_a_validation_error_named_after_it(string query, string parameter)
    {
        var client = await SaveUserAsync("ana@example.com", UserRole.Client);

        var response = await GetTicketsAsync(TokenFor(client), query);

        var errors = (await AssertProblemAsync(response, HttpStatusCode.BadRequest)).GetProperty("errors");
        Assert.Equal([parameter], errors.EnumerateObject().Select(error => error.Name));
    }

    [Fact]
    public async Task Search_longer_than_200_characters_is_a_validation_error()
    {
        var client = await SaveUserAsync("ana@example.com", UserRole.Client);

        var response = await GetTicketsAsync(TokenFor(client), $"search={new string('s', 201)}");

        var errors = (await AssertProblemAsync(response, HttpStatusCode.BadRequest)).GetProperty("errors");
        Assert.Equal(["search"], errors.EnumerateObject().Select(error => error.Name));
    }

    [Theory]
    [InlineData("page=abc", "page")]
    [InlineData("pageSize=1.5", "pageSize")]
    [InlineData("categoryId=xyz", "categoryId")]
    [InlineData("assignedToId=xyz", "assignedToId")]
    public async Task Value_that_cannot_be_converted_is_rejected_by_binding(string query, string parameter)
    {
        var client = await SaveUserAsync("ana@example.com", UserRole.Client);

        var response = await GetTicketsAsync(TokenFor(client), query);

        var problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        var error = Assert.Single(problem.GetProperty("errors").EnumerateObject());
        Assert.Equal(parameter, error.Name);
        Assert.DoesNotContain("Helpdesk", problem.GetRawText());
    }

    [Fact]
    public async Task Anonymous_request_is_rejected()
    {
        var response = await _client.GetAsync("/api/tickets");

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Validly_signed_token_with_an_invalid_role_lists_nothing()
    {
        var client = await SaveUserAsync("ana@example.com", UserRole.Client);
        await SaveTicketAsync(client.Id);

        var response = await GetTicketsAsync(SignedTokenWithRole(client.Id, "0"), "");

        // A token the API issued always has a valid role, so this is a server fault, answered without details.
        var problem = await AssertProblemAsync(response, HttpStatusCode.InternalServerError);
        Assert.False(problem.TryGetProperty("items", out _));
        Assert.DoesNotContain("role", problem.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    private async Task<PagedResponse<TicketListItem>> ListAsync(User user, string query = "")
    {
        var response = await GetTicketsAsync(TokenFor(user), query);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PagedResponse<TicketListItem>>())!;
    }

    private Task<HttpResponseMessage> GetTicketsAsync(string accessToken, string query)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/tickets?{query}");
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

    private async Task<User> SaveUserAsync(string email, UserRole role)
    {
        await using var context = PostgreSqlFixture.CreateContext(_connectionString);
        var user = new User("Test User", email, "hash", role);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private async Task<Ticket> SaveTicketAsync(
        Guid creatorId,
        string title = "Printer offline",
        TicketPriority priority = TicketPriority.Medium,
        Guid? categoryId = null,
        TicketStatus status = TicketStatus.Open,
        Guid? assignedToId = null)
    {
        await using var context = PostgreSqlFixture.CreateContext(_connectionString);
        var ticket = new Ticket(title, "It shows error 42.", priority, categoryId ?? _generalId, creatorId, UtcNow);
        context.Tickets.Add(ticket);

        // Set directly instead of through Assign and ChangeStatus, so a test can start from any status
        // without walking the transition table.
        context.Entry(ticket).Property(t => t.Status).CurrentValue = status;
        context.Entry(ticket).Property(t => t.AssignedToId).CurrentValue = assignedToId;

        await context.SaveChangesAsync();
        return ticket;
    }

    private async Task SaveTicketsAsync(Guid creatorId, int count)
    {
        await using var context = PostgreSqlFixture.CreateContext(_connectionString);
        for (var i = 0; i < count; i++)
        {
            context.Tickets.Add(new Ticket($"Ticket {i}", "It shows error 42.", TicketPriority.Medium, _generalId,
                creatorId, UtcNow.AddMinutes(-i)));
        }

        await context.SaveChangesAsync();
    }
}
