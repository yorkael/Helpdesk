using Helpdesk.Api.Authentication;
using Helpdesk.Application.Agents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Helpdesk.Api.Controllers;

/// <remarks>
/// Admins only. The policy is checked before the action runs, so agents and clients cause no query.
/// </remarks>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
[Route("api/agents")]
public sealed class AgentsController(AgentService agentService) : ControllerBase
{
    /// <summary>
    /// The active agents with their id and name, ordered by name, so an admin can pick one to assign a ticket to.
    /// Not paginated.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AgentResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var agents = await agentService.ListAsync(User.GetUserRole(), cancellationToken);
        return Ok(agents);
    }
}
