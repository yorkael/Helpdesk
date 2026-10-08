using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Helpdesk.Application.Abstractions;
using Helpdesk.Application.Categories;
using Helpdesk.Domain.Entities;
using Helpdesk.Domain.Enums;
using Helpdesk.Tests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Helpdesk.Tests.Api.ProblemAssertions;

namespace Helpdesk.Tests.Api;

/// <summary>
/// Covers listing categories over HTTP: open to every role but not to anonymous callers, ordered by name with
/// ICU's root collation, and only id and name in the body.
/// </summary>
[Collection(PostgreSqlCollection.Name)]
public class CategoryEndpointsTests(PostgreSqlFixture fixture) : IAsyncLifetime
{
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

    [Theory]
    [InlineData(UserRole.Client)]
    [InlineData(UserRole.Agent)]
    [InlineData(UserRole.Admin)]
    public async Task Any_authenticated_role_can_list_categories(UserRole role)
    {
        var caller = await SaveUserAsync(role);

        var response = await GetCategoriesAsync(caller);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            await ExpectedAsync("Account", "Billing", "General", "Technical issue"),
            await response.Content.ReadFromJsonAsync<List<CategoryResponse>>());
    }

    [Fact]
    public async Task Categories_are_ordered_by_name_with_the_icu_root_collation()
    {
        // Inserted out of order. The expected order differs from the order by id or insertion, from the server's
        // libc locale (which ignores the space: "Accounts" before "Account settings") and from "C" (byte order puts
        // lowercase and accented names last).
        await SaveCategoriesAsync("Accounts", "email", "Órdenes", "Account settings");
        var caller = await SaveUserAsync(UserRole.Client);

        var response = await GetCategoriesAsync(caller);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            await ExpectedAsync(
                "Account", "Account settings", "Accounts", "Billing", "email", "General", "Órdenes", "Technical issue"),
            await response.Content.ReadFromJsonAsync<List<CategoryResponse>>());
    }

    [Fact]
    public async Task Empty_catalog_returns_an_empty_array()
    {
        await using (var context = PostgreSqlFixture.CreateContext(_connectionString))
        {
            await context.Categories.ExecuteDeleteAsync();
        }

        var caller = await SaveUserAsync(UserRole.Client);

        var response = await GetCategoriesAsync(caller);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("[]", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Anonymous_request_is_rejected()
    {
        var response = await _client.GetAsync("/api/categories");

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Categories_have_only_id_and_name_in_camel_case()
    {
        var caller = await SaveUserAsync(UserRole.Client);

        var response = await GetCategoriesAsync(caller);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var categories = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.EnumerateArray().ToList();
        Assert.NotEmpty(categories);
        Assert.All(categories, category =>
            Assert.Equal(["id", "name"], category.EnumerateObject().Select(property => property.Name)));
    }

    private Task<HttpResponseMessage> GetCategoriesAsync(User caller)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/categories");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TokenFor(caller));
        return _client.SendAsync(request);
    }

    private string TokenFor(User user) =>
        _factory.Services.GetRequiredService<IAccessTokenIssuer>().Issue(user, DateTimeOffset.UtcNow).Value;

    // The names, in the expected order, paired with the ids stored for them.
    private async Task<List<CategoryResponse>> ExpectedAsync(params string[] names)
    {
        await using var context = PostgreSqlFixture.CreateContext(_connectionString);
        var ids = await context.Categories.ToDictionaryAsync(category => category.Name, category => category.Id);
        return names.Select(name => new CategoryResponse(ids[name], name)).ToList();
    }

    private async Task SaveCategoriesAsync(params string[] names)
    {
        // One save per name, so the rows are inserted in the order given.
        foreach (var name in names)
        {
            await using var context = PostgreSqlFixture.CreateContext(_connectionString);
            context.Categories.Add(new Category(name));
            await context.SaveChangesAsync();
        }
    }

    private async Task<User> SaveUserAsync(UserRole role)
    {
        await using var context = PostgreSqlFixture.CreateContext(_connectionString);
        var user = new User($"{role} User", $"{role.ToString().ToLowerInvariant()}@example.com", "hash", role);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }
}
