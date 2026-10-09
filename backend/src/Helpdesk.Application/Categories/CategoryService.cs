using Helpdesk.Application.Abstractions;

namespace Helpdesk.Application.Categories;

/// <summary>
/// Lists the categories a ticket can be filed under. Any authenticated user may read them.
/// Read only: nothing is saved, and reads are not audited.
/// </summary>
public sealed class CategoryService(ICategoryRepository categories)
{
    /// <returns>Every category, ordered by name. Not paginated.</returns>
    public Task<IReadOnlyList<CategoryResponse>> ListAsync(CancellationToken cancellationToken) =>
        categories.ListAsync(cancellationToken);
}
