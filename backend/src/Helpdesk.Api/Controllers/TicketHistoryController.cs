using Helpdesk.Api.Authentication;
using Helpdesk.Application.Tickets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Helpdesk.Api.Controllers;

/// <remarks>
/// Admins only. The role is checked before the ticket is looked up, so a 403 reveals nothing about which
/// tickets exist.
/// </remarks>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
[Route("api/tickets/{id:guid}/history")]
public sealed class TicketHistoryController(TicketHistoryService historyService) : ControllerBase
{
    /// <summary>
    /// The ticket's audit history, oldest first, with values as stored: status names, and user ids for the
    /// assignee with their names alongside. Not paginated.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<TicketHistoryEntryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> List(Guid id, CancellationToken cancellationToken)
    {
        var history = await historyService.ListAsync(id, User.GetUserId(), User.GetUserRole(), cancellationToken);
        return Ok(history);
    }
}
