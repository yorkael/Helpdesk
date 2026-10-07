using Helpdesk.Api.Authentication;
using Helpdesk.Application.Tickets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Helpdesk.Api.Controllers;

/// <remarks>
/// Open to every role: the caller's role, read from the access token, decides which tickets they may comment on
/// and whether they see internal comments. A ticket the caller may not see answers 404, like one that does not
/// exist, so its existence is not revealed.
/// </remarks>
[ApiController]
[Authorize]
[Route("api/tickets/{id:guid}/comments")]
public sealed class TicketCommentsController(TicketCommentService commentService) : ControllerBase
{
    /// <summary>
    /// The author comes from the access token. Only support staff may write internal comments, and a closed
    /// ticket receives none. No Location header: there is no endpoint for a single comment.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<CommentResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(Guid id, CreateCommentRequest request, CancellationToken cancellationToken)
    {
        var comment = await commentService.AddAsync(id, request, User.GetUserId(), User.GetUserRole(), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, comment);
    }

    /// <summary>The ticket's comments, oldest first; clients only get public ones. Not paginated.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CommentResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> List(Guid id, CancellationToken cancellationToken)
    {
        var comments = await commentService.ListAsync(id, User.GetUserId(), User.GetUserRole(), cancellationToken);
        return Ok(comments);
    }
}
