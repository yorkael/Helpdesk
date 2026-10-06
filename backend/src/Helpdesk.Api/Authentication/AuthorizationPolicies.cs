namespace Helpdesk.Api.Authentication;

public static class AuthorizationPolicies
{
    public const string AdminOnly = nameof(AdminOnly);

    /// <summary>Support staff: admins and agents.</summary>
    public const string StaffOnly = nameof(StaffOnly);

    public const string ClientOnly = nameof(ClientOnly);
}
