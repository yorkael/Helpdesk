using Helpdesk.Application.Abstractions;
using Helpdesk.Application.Tickets;
using Helpdesk.Domain.Enums;

namespace Helpdesk.Application.Agents;

/// <summary>
/// Lists the agents a ticket can be assigned to. Admins only; the role is checked before the query.
/// Read only: nothing is saved, and reads are not audited.
/// </summary>
public sealed class AgentService(IUserRepository users)
{
    /// <param name="role">The authenticated user's role; only admins may list agents.</param>
    /// <returns>The active agents, ordered by name. Not paginated.</returns>
    /// <exception cref="TicketActionForbiddenException">The caller is not an admin.</exception>
    public async Task<IReadOnlyList<AgentResponse>> ListAsync(UserRole role, CancellationToken cancellationToken)
    {
        EnsureAdmin(role);
        return await users.ListActiveAgentsAsync(cancellationToken);
    }

    // The API's admin policy already stops everyone else; repeated here so the service is safe on its own.
    private static void EnsureAdmin(UserRole role)
    {
        if (role != UserRole.Admin)
        {
            throw new TicketActionForbiddenException("Only admins can list agents.");
        }
    }
}
