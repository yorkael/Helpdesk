using Helpdesk.Application.Categories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Helpdesk.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/categories")]
public sealed class CategoriesController(CategoryService categoryService) : ControllerBase
{
    /// <summary>
    /// Every category with its id and name, ordered by name, so clients can pick one when filing a ticket.
    /// Not paginated.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CategoryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var categories = await categoryService.ListAsync(cancellationToken);
        return Ok(categories);
    }
}
