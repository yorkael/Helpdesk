using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Helpdesk.Application.Authentication;
using Helpdesk.Infrastructure.Authentication;
using Helpdesk.Tests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Helpdesk.Tests.Api;

[Collection(PostgreSqlCollection.Name)]
public class AuthEndpointsTests(PostgreSqlFixture fixture) : IAsyncLifetime
{
    private const string Password = "correct horse battery";

    private string _connectionString = null!;
    private HelpdeskApiFactory _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _connectionString = await fixture.CreateMigratedDatabaseAsync();
        _factory = new HelpdeskApiFactory(_connectionString);
        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Registered_client_logs_in_and_the_access_token_identifies_them_with_their_role()
    {
        var registerResponse = await RegisterAsync("Ana Ruiz", " Ana@Example.com ", Password);
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        var registered = await registerResponse.Content.ReadFromJsonAsync<UserResponse>();
        Assert.Equal("ana@example.com", registered!.Email);
        Assert.Equal("Client", registered.Role);

        var tokens = await LoginAsync("ana@example.com", Password);
        var me = await GetMeAsync(tokens.AccessToken);

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Equal(registered, await me.Content.ReadFromJsonAsync<UserResponse>());
    }

    [Fact]
    public async Task Refresh_rotates_the_tokens_and_reusing_the_old_one_ends_the_session()
    {
        await RegisterAsync("Ana Ruiz", "ana@example.com", Password);
        var login = await LoginAsync("ana@example.com", Password);

        var refreshResponse = await RefreshAsync(login.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        var refreshed = (await refreshResponse.Content.ReadFromJsonAsync<AuthTokensResponse>())!;
        Assert.NotEqual(login.RefreshToken, refreshed.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, (await GetMeAsync(refreshed.AccessToken)).StatusCode);

        await AssertProblemAsync(await RefreshAsync(login.RefreshToken), HttpStatusCode.Unauthorized);
        await AssertProblemAsync(await RefreshAsync(refreshed.RefreshToken), HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_returns_204_and_the_refresh_token_stops_working()
    {
        await RegisterAsync("Ana Ruiz", "ana@example.com", Password);
        var login = await LoginAsync("ana@example.com", Password);

        var logout = await _client.PostAsJsonAsync("/api/auth/logout", new RefreshTokenRequest(login.RefreshToken));

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        await AssertProblemAsync(await RefreshAsync(login.RefreshToken), HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_returns_204_for_an_unknown_or_already_revoked_token()
    {
        await RegisterAsync("Ana Ruiz", "ana@example.com", Password);
        var login = await LoginAsync("ana@example.com", Password);
        await _client.PostAsJsonAsync("/api/auth/logout", new RefreshTokenRequest(login.RefreshToken));

        var again = await _client.PostAsJsonAsync("/api/auth/logout", new RefreshTokenRequest(login.RefreshToken));
        var unknown = await _client.PostAsJsonAsync("/api/auth/logout", new RefreshTokenRequest("never-issued"));

        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, unknown.StatusCode);
    }

    [Fact]
    public async Task Logout_without_a_token_is_a_validation_error()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/logout", new { });

        var problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.True(problem.GetProperty("errors").TryGetProperty("refreshToken", out _));
    }

    [Fact]
    public async Task Unknown_email_and_wrong_password_return_the_same_401_problem()
    {
        await RegisterAsync("Ana Ruiz", "ana@example.com", Password);

        var unknownEmail = await AssertProblemAsync(
            await PostLoginAsync("nobody@example.com", Password), HttpStatusCode.Unauthorized);
        var wrongPassword = await AssertProblemAsync(
            await PostLoginAsync("ana@example.com", "wrong password"), HttpStatusCode.Unauthorized);

        AssertSameProblem(unknownEmail, wrongPassword);
        Assert.Equal("Invalid email or password.", wrongPassword.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Inactive_user_cannot_log_in_or_refresh()
    {
        await RegisterAsync("Ana Ruiz", "ana@example.com", Password);
        var login = await LoginAsync("ana@example.com", Password);
        await using (var context = PostgreSqlFixture.CreateContext(_connectionString))
        {
            await context.Database.ExecuteSqlAsync($"UPDATE users SET is_active = false WHERE email = 'ana@example.com'");
        }

        var inactiveLogin = await AssertProblemAsync(
            await PostLoginAsync("ana@example.com", Password), HttpStatusCode.Unauthorized);
        var wrongPassword = await AssertProblemAsync(
            await PostLoginAsync("ana@example.com", "wrong password"), HttpStatusCode.Unauthorized);

        AssertSameProblem(inactiveLogin, wrongPassword);
        await AssertProblemAsync(await RefreshAsync(login.RefreshToken), HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Invalid_registration_returns_a_validation_problem_from_fluentvalidation()
    {
        var response = await RegisterAsync("Ana Ruiz", "not-an-email", "short");

        var errors = (await AssertProblemAsync(response, HttpStatusCode.BadRequest)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("email", out _));
        Assert.True(errors.TryGetProperty("password", out _));
        Assert.False(errors.TryGetProperty("name", out _));
    }

    [Fact]
    public async Task Missing_fields_are_reported_once_each_by_fluentvalidation()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new { });

        var errors = (await AssertProblemAsync(response, HttpStatusCode.BadRequest)).GetProperty("errors");
        Assert.Equal(["email", "name", "password"], errors.EnumerateObject().Select(error => error.Name).Order());
    }

    [Fact]
    public async Task Malformed_json_returns_a_400_problem()
    {
        var response = await _client.PostAsync(
            "/api/auth/login",
            new StringContent("{ not json", Encoding.UTF8, "application/json"));

        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Registering_an_existing_email_returns_a_409_problem()
    {
        await RegisterAsync("Ana Ruiz", "ana@example.com", Password);

        var response = await RegisterAsync("Ana Again", "ANA@example.com", Password);

        await AssertProblemAsync(response, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Me_without_a_token_is_rejected_by_the_fallback_policy()
    {
        var response = await _client.GetAsync("/api/auth/me");

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_signed_with_another_key_is_rejected()
    {
        var forged = CreateToken(new SigningCredentials(
            new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32)), SecurityAlgorithms.HmacSha256));

        await AssertProblemAsync(await GetMeAsync(forged), HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Unsigned_token_is_rejected()
    {
        var unsigned = CreateToken(signingCredentials: null);

        await AssertProblemAsync(await GetMeAsync(unsigned), HttpStatusCode.Unauthorized);
    }

    private Task<HttpResponseMessage> RegisterAsync(string name, string email, string password) =>
        _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(name, email, password));

    private Task<HttpResponseMessage> PostLoginAsync(string email, string password) =>
        _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));

    private async Task<AuthTokensResponse> LoginAsync(string email, string password)
    {
        var response = await PostLoginAsync(email, password);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AuthTokensResponse>())!;
    }

    private Task<HttpResponseMessage> RefreshAsync(string refreshToken) =>
        _client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequest(refreshToken));

    private Task<HttpResponseMessage> GetMeAsync(string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return _client.SendAsync(request);
    }

    /// <summary>A token with valid issuer, audience, lifetime and claims; only its signature is wrong.</summary>
    private static string CreateToken(SigningCredentials? signingCredentials) =>
        new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "helpdesk-tests",
            Audience = "helpdesk-tests-client",
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = signingCredentials,
            Claims = new Dictionary<string, object>
            {
                [AccessTokenClaimTypes.Subject] = Guid.NewGuid().ToString(),
                [AccessTokenClaimTypes.Email] = "intruder@example.com",
                [AccessTokenClaimTypes.Name] = "Intruder",
                [AccessTokenClaimTypes.Role] = "Admin"
            }
        });

    private static async Task<JsonElement> AssertProblemAsync(HttpResponseMessage response, HttpStatusCode expectedStatus)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((int)expectedStatus, problem.GetProperty("status").GetInt32());
        return problem;
    }

    // traceId differs per request by design; every other member must match.
    private static void AssertSameProblem(JsonElement expected, JsonElement actual)
    {
        static Dictionary<string, string> WithoutTraceId(JsonElement problem) => problem.EnumerateObject()
            .Where(member => member.Name != "traceId")
            .ToDictionary(member => member.Name, member => member.Value.GetRawText());

        Assert.Equal(WithoutTraceId(expected), WithoutTraceId(actual));
    }
}
