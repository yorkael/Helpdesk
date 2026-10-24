using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Helpdesk.Application.Abstractions;
using Helpdesk.Application.Agents;
using Helpdesk.Domain.Entities;
using Helpdesk.Domain.Enums;
using Helpdesk.Tests.Application;
using Helpdesk.Tests.Persistence;
using Microsoft.Extensions.DependencyInjection;
using static Helpdesk.Tests.Api.ProblemAssertions;

namespace Helpdesk.Tests.Api;

/// <summary>
/// Covers listing agents over HTTP: admins only, with agents and clients stopped by the policy; only active agents,
/// ordered by name with ICU's root collation; and only id and name in the body.
/// </summary>
[Collection(PostgreSqlCollection.Name)]
public class AgentEndpointsTests(PostgreSqlFixture fixture) : IAsyncLifetime
{
    private string _connectionString = null!;
    private HelpdeskApiFactory _factory = null!;
    private HttpClient _client = null!;
    private User _admin = null!;

    public async Task InitializeAsync()
    {
        _connectionString = await fixture.CreateMigratedDatabaseAsync();
        _factory = new HelpdeskApiFactory(_connectionString);
        _client = _factory.CreateClient();
        _admin = await SaveUserAsync("Admin User", "admin@example.com", UserRole.Admin);
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Admin_gets_the_active_agents_ordered_by_name_with_the_icu_root_collation()
    {
        // Inserted out of order; the database default (en_US.utf8) would put "Anabel" before "Ana María".
        var saved = new Dictionary<string, User>();
        foreach (var name in new[] { "Zoe", "Anabel", "ana", "Ana María", "Álvaro" })
        {
            saved[name] = await SaveUserAsync(name, $"agent{saved.Count}@example.com", UserRole.Agent);
        }

        var response = await GetAgentsAsync(TokenFor(_admin));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            new[] { "Álvaro", "ana", "Ana María", "Anabel", "Zoe" }
                .Select(name => new AgentResponse(saved[name].Id, name)),
            await response.Content.ReadFromJsonAsync<List<AgentResponse>>());
    }

    [Fact]
    public async Task Clients_admins_and_inactive_agents_are_left_out()
    {
        var client = await SaveUserAsync("Ana Client", "ana@example.com", UserRole.Client);
        var otherAdmin = await SaveUserAsync("Other Admin", "other.admin@example.com", UserRole.Admin);
        var inactiveAgent = await SaveUserAsync("Old Agent", "old@example.com", UserRole.Agent, isActive: false);
        var agent = await SaveUserAsync("Eva Agent", "eva@example.com", UserRole.Agent);

        var response = await GetAgentsAsync(TokenFor(_admin));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var agents = (await response.Content.ReadFromJsonAsync<List<AgentResponse>>())!;
        Assert.Equal([new AgentResponse(agent.Id, "Eva Agent")], agents);
        foreach (var excluded in new[] { client, _admin, otherAdmin, inactiveAgent })
        {
            Assert.DoesNotContain(agents, listed => listed.Id == excluded.Id);
        }
    }

    [Fact]
    public async Task Agents_have_only_id_and_name_in_camel_case_and_no_email()
    {
        var agent = await SaveUserAsync("Eva Agent", "eva@example.com", UserRole.Agent);

        var response = await GetAgentsAsync(TokenFor(_admin));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var listed = Assert.Single(JsonDocument.Parse(json).RootElement.EnumerateArray());
        Assert.Equal(["id", "name"], listed.EnumerateObject().Select(property => property.Name));
        Assert.DoesNotContain("@", json);
        Assert.DoesNotContain(agent.Email, json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task No_agents_return_an_empty_array()
    {
        var response = await GetAgentsAsync(TokenFor(_admin));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("[]", await response.Content.ReadAsStringAsync());
    }

    // The service also rejects non-admins with a 403, but with a detail. No detail means the policy stopped the call.
    [Theory]
    [InlineData(UserRole.Agent)]
    [InlineData(UserRole.Client)]
    public async Task Non_admin_is_forbidden_by_the_policy(UserRole role)
    {
        var caller = await SaveCallerAsync(role);

        var response = await GetAgentsAsync(TokenFor(caller));

        var problem = await AssertProblemAsync(response, HttpStatusCode.Forbidden);
        Assert.False(problem.TryGetProperty("detail", out _));
    }

    [Fact]
    public async Task Agent_and_client_get_the_same_forbidden_body()
    {
        var agent = await SaveCallerAsync(UserRole.Agent);
        var client = await SaveCallerAsync(UserRole.Client);

        var agentProblem = await AssertProblemAsync(await GetAgentsAsync(TokenFor(agent)), HttpStatusCode.Forbidden);
        var clientProblem = await AssertProblemAsync(await GetAgentsAsync(TokenFor(client)), HttpStatusCode.Forbidden);

        Assert.Equal(WithoutTraceId(clientProblem), WithoutTraceId(agentProblem));
    }

    [Fact]
    public async Task Anonymous_request_is_rejected()
    {
        var response = await _client.GetAsync("/api/agents");

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_with_a_tampered_signature_is_rejected()
    {
        // The first character of the signature carries six significant bits, so changing it changes the signature.
        var parts = TokenFor(_admin).Split('.');
        parts[2] = (parts[2][0] == 'A' ? 'B' : 'A') + parts[2][1..];

        var response = await GetAgentsAsync(string.Join('.', parts));

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized);
    }

    private Task<HttpResponseMessage> GetAgentsAsync(string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/agents");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return _client.SendAsync(request);
    }

    private string TokenFor(User user) =>
        _factory.Services.GetRequiredService<IAccessTokenIssuer>().Issue(user, DateTimeOffset.UtcNow).Value;

    // The trace id differs on every request; everything else must match.
    private static Dictionary<string, string> WithoutTraceId(JsonElement problem) =>
        problem.EnumerateObject()
            .Where(property => property.Name != "traceId")
            .ToDictionary(property => property.Name, property => property.Value.GetRawText());

    private Task<User> SaveCallerAsync(UserRole role) =>
        SaveUserAsync($"{role} User", $"{role.ToString().ToLowerInvariant()}@example.com", role);

    private async Task<User> SaveUserAsync(string name, string email, UserRole role, bool isActive = true)
    {
        await using var context = PostgreSqlFixture.CreateContext(_connectionString);
        var user = new User(name, email, "hash", role);
        if (!isActive)
        {
            TestUsers.Deactivate(user);
        }

        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }
}
