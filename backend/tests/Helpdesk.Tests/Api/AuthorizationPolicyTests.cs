using System.Security.Claims;
using Helpdesk.Api.Authentication;
using Helpdesk.Infrastructure.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Helpdesk.Tests.Api;

public class AuthorizationPolicyTests
{
    private readonly IServiceProvider _services = new ServiceCollection()
        .AddLogging()
        .AddRoleAuthorization()
        .BuildServiceProvider();

    [Theory]
    [InlineData(AuthorizationPolicies.AdminOnly, "Admin", true)]
    [InlineData(AuthorizationPolicies.AdminOnly, "Agent", false)]
    [InlineData(AuthorizationPolicies.AdminOnly, "Client", false)]
    [InlineData(AuthorizationPolicies.StaffOnly, "Admin", true)]
    [InlineData(AuthorizationPolicies.StaffOnly, "Agent", true)]
    [InlineData(AuthorizationPolicies.StaffOnly, "Client", false)]
    [InlineData(AuthorizationPolicies.ClientOnly, "Admin", false)]
    [InlineData(AuthorizationPolicies.ClientOnly, "Agent", false)]
    [InlineData(AuthorizationPolicies.ClientOnly, "Client", true)]
    public async Task Policy_allows_only_its_roles(string policy, string role, bool expected)
    {
        var result = await AuthorizeAsync(UserWithRole(role), policy);

        Assert.Equal(expected, result.Succeeded);
    }

    [Fact]
    public async Task Fallback_policy_requires_an_authenticated_user()
    {
        var fallbackPolicy = await _services.GetRequiredService<IAuthorizationPolicyProvider>().GetFallbackPolicyAsync();
        var authorization = _services.GetRequiredService<IAuthorizationService>();

        var anonymous = await authorization.AuthorizeAsync(new ClaimsPrincipal(new ClaimsIdentity()), fallbackPolicy!);
        var authenticated = await authorization.AuthorizeAsync(UserWithRole("Client"), fallbackPolicy!);

        Assert.False(anonymous.Succeeded);
        Assert.True(authenticated.Succeeded);
    }

    private Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, string policy) =>
        _services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, policy);

    // Same role claim type the JWT bearer handler is configured with.
    private static ClaimsPrincipal UserWithRole(string role) => new(new ClaimsIdentity(
        [new Claim(AccessTokenClaimTypes.Role, role)],
        authenticationType: "Test",
        nameType: AccessTokenClaimTypes.Name,
        roleType: AccessTokenClaimTypes.Role));
}
