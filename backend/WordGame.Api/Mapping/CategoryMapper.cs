using WordGame.Api.Contracts.V1;
using WordGame.Domain;

namespace WordGame.Api.Mapping;

internal static class CategoryMapper
{
    internal static CategoryResponse Map(Category category) => new(category.Id, category.Name, category.Difficulty switch
    {
        CategoryDifficulty.Easy => ApiCategoryDifficulty.Easy,
        CategoryDifficulty.Medium => ApiCategoryDifficulty.Medium,
        CategoryDifficulty.Hard => ApiCategoryDifficulty.Hard,
        _ => throw new InvalidOperationException("Unsupported category difficulty.")
    });
}
