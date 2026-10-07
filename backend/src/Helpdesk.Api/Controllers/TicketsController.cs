using Helpdesk.Api.Authentication;
using Helpdesk.Application.Tickets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Helpdesk.Api.Controllers;

[ApiController]
[Route("api/tickets")]
public sealed class TicketsController(TicketService ticketService) : ControllerBase
{
    /// <summary>The creator comes from the access token; assignee and status cannot be sent.</summary>
    [Authorize(Policy = AuthorizationPolicies.ClientOnly)]
    [HttpPost]
    [ProducesResponseType<TicketResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(CreateTicketRequest request, CancellationToken cancellationToken)
    {
        var ticket = await ticketService.CreateAsync(request, User.GetUserId(), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ticket);
    }

    /// <summary>
    /// The caller's role, read from the access token, decides which tickets they may see; the filters only
    /// narrow that set. There is no creator parameter, and unknown parameters are ignored.
    /// </summary>
    /// <remarks>
    /// Bound as separate parameters, not as one object, so a value that cannot be converted is reported under
    /// the same name the client sent (page, not Page), like the errors from FluentValidation.
    /// </remarks>
    [Authorize]
    [HttpGet]
    [ProducesResponseType<PagedResponse<TicketListItem>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> List(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] string? status,
        [FromQuery] string? priority,
        [FromQuery] Guid? assignedToId,
        [FromQuery] Guid? categoryId,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var request = new ListTicketsRequest(page, pageSize, status, priority, assignedToId, categoryId, search);
        var tickets = await ticketService.ListAsync(request, User.GetUserId(), User.GetUserRole(), cancellationToken);
        return Ok(tickets);
    }
}
