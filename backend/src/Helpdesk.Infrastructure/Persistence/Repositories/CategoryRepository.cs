using Helpdesk.Application.Abstractions;
using Helpdesk.Application.Categories;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.Infrastructure.Persistence.Repositories;

internal sealed class CategoryRepository(HelpdeskDbContext context) : ICategoryRepository
{
    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken) =>
        context.Categories.AnyAsync(category => category.Id == id, cancellationToken);

    /// <summary>Only id and name are read; the projection is not tracked.</summary>
    public async Task<IReadOnlyList<CategoryResponse>> ListAsync(CancellationToken cancellationToken) =>
        await context.Categories
            .OrderBy(category => EF.Functions.Collate(category.Name, Collations.NameOrder))
            .Select(category => new CategoryResponse(category.Id, category.Name))
            .ToListAsync(cancellationToken);
}
