using Helpdesk.Application.Abstractions;
using Helpdesk.Application.Categories;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.Infrastructure.Persistence.Repositories;

internal sealed class CategoryRepository(HelpdeskDbContext context) : ICategoryRepository
{
    // ICU's root collation sorts the same on every server; the database default follows the server's OS locale.
    private const string NameCollation = "und-x-icu";

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken) =>
        context.Categories.AnyAsync(category => category.Id == id, cancellationToken);

    /// <summary>Only id and name are read; the projection is not tracked.</summary>
    public async Task<IReadOnlyList<CategoryResponse>> ListAsync(CancellationToken cancellationToken) =>
        await context.Categories
            .OrderBy(category => EF.Functions.Collate(category.Name, NameCollation))
            .Select(category => new CategoryResponse(category.Id, category.Name))
            .ToListAsync(cancellationToken);
}
