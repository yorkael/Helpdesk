using System.Security.Claims;
using Helpdesk.Api.Authentication;
using Helpdesk.Domain.Enums;
using Helpdesk.Infrastructure.Authentication;

namespace Helpdesk.Tests.Api;

public class ClaimsPrincipalExtensionsTests
{
    [Theory]
    [InlineData("Admin", UserRole.Admin)]
    [InlineData("Agent", UserRole.Agent)]
    [InlineData("Client", UserRole.Client)]
    public void Role_is_read_from_the_role_claim(string claimValue, UserRole expected)
    {
        Assert.Equal(expected, UserWithRoles(claimValue).GetUserRole());
    }

    [Fact]
    public void Missing_role_claim_fails_instead_of_using_a_default()
    {
        Assert.Throws<InvalidOperationException>(() => UserWithRoles().GetUserRole());
    }

    [Theory]
    [InlineData("")]
    [InlineData("admin")]
    [InlineData("0")]
    [InlineData("Admin,Client")]
    [InlineData("SuperUser")]
    public void Role_that_is_not_an_exact_role_name_fails(string claimValue)
    {
        Assert.Throws<InvalidOperationException>(() => UserWithRoles(claimValue).GetUserRole());
    }

    [Fact]
    public void More_than_one_role_claim_fails()
    {
        Assert.Throws<InvalidOperationException>(() => UserWithRoles("Client", "Admin").GetUserRole());
    }

    private static ClaimsPrincipal UserWithRoles(params string[] roles) =>
        new(new ClaimsIdentity(
            roles.Select(role => new Claim(AccessTokenClaimTypes.Role, role)),
            authenticationType: "Test",
            nameType: AccessTokenClaimTypes.Name,
            roleType: AccessTokenClaimTypes.Role));
}
