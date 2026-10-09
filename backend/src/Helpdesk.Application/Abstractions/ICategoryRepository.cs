using Helpdesk.Application.Categories;

namespace Helpdesk.Application.Abstractions;

public interface ICategoryRepository
{
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken);

    /// <returns>
    /// Every category, ordered by name with the Unicode root collation: case and accents do not split letters apart,
    /// and a shorter prefix comes first. Names are unique, so the order is always the same.
    /// </returns>
    Task<IReadOnlyList<CategoryResponse>> ListAsync(CancellationToken cancellationToken);
}
