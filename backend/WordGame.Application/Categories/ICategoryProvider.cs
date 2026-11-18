using WordGame.Domain;

namespace WordGame.Application.Categories;

public interface ICategoryProvider
{
    // Returns an immutable metadata catalog, optionally filtered by difficulty.
    Task<IReadOnlyList<Category>> GetCategoriesAsync(
        CategoryDifficulty? difficulty = null,
        CancellationToken cancellationToken = default);

    // Null means unknown category. Invalid data/provider failures must not be treated as "unknown".
    Task<CategoryDefinition?> GetByIdAsync(string categoryId, CancellationToken cancellationToken = default);
}
