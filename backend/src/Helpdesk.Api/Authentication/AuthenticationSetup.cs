using Helpdesk.Domain.Enums;
using Helpdesk.Infrastructure.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

namespace Helpdesk.Api.Authentication;

public static class AuthenticationSetup
{
    // The default of five minutes would stretch a 15-minute token by a third.
    private static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(30);

    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<JwtOptions>((bearer, jwt) =>
            {
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = jwt.SigningKey,
                    // Pinning the algorithm blocks tokens that claim "none" or a different algorithm.
                    ValidAlgorithms = [JwtOptions.SigningAlgorithm],
                    ClockSkew = ClockSkew,
                    NameClaimType = AccessTokenClaimTypes.Name,
                    RoleClaimType = AccessTokenClaimTypes.Role
                };
            });

        return services.AddRoleAuthorization();
    }

    public static IServiceCollection AddRoleAuthorization(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            // Secure by default: an endpoint without [Authorize] or [AllowAnonymous] still needs a valid token.
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(AuthorizationPolicies.AdminOnly, policy => policy.RequireRole(nameof(UserRole.Admin)))
            .AddPolicy(AuthorizationPolicies.StaffOnly, policy => policy.RequireRole(nameof(UserRole.Admin), nameof(UserRole.Agent)))
            .AddPolicy(AuthorizationPolicies.ClientOnly, policy => policy.RequireRole(nameof(UserRole.Client)));

        return services;
    }
}
