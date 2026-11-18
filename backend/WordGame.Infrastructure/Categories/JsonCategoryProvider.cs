using System.Collections.Frozen;
using WordGame.Application.Categories;
using WordGame.Domain;

namespace WordGame.Infrastructure.Categories;

public sealed class JsonCategoryProvider : ICategoryProvider
{
    private readonly FrozenDictionary<string, CategoryDefinition> definitions;
    private readonly IReadOnlyList<Category> catalog;
    private readonly FrozenDictionary<CategoryDifficulty, IReadOnlyList<Category>> catalogsByDifficulty;

    public JsonCategoryProvider(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        var files = Directory.GetFiles(Path.GetFullPath(directoryPath), "*.json", SearchOption.TopDirectoryOnly);
        if (files.Length == 0)
        {
            throw new InvalidDataException($"No category JSON files were found in '{directoryPath}'.");
        }

        var loaded = new Dictionary<string, CategoryDefinition>(StringComparer.Ordinal);
        foreach (var file in files.Order(StringComparer.Ordinal))
        {
            var definition = JsonCategoryFile.Load(file);
            if (!loaded.TryAdd(definition.Category.Id, definition))
            {
                throw new InvalidDataException($"Duplicate category ID '{definition.Category.Id}' in '{file}'.");
            }
        }

        definitions = loaded.ToFrozenDictionary(StringComparer.Ordinal);
        catalog = Array.AsReadOnly(loaded.Values.Select(definition => definition.Category)
            .OrderBy(category => category.Id, StringComparer.Ordinal).ToArray());
        catalogsByDifficulty = Enum.GetValues<CategoryDifficulty>().ToFrozenDictionary(
            difficulty => difficulty,
            difficulty => (IReadOnlyList<Category>)Array.AsReadOnly(
                catalog.Where(category => category.Difficulty == difficulty).ToArray()));
    }

    public Task<IReadOnlyList<Category>> GetCategoriesAsync(
        CategoryDifficulty? difficulty = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (difficulty.HasValue && !Enum.IsDefined(difficulty.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(difficulty));
        }

        return Task.FromResult(difficulty.HasValue ? catalogsByDifficulty[difficulty.Value] : catalog);
    }

    public Task<CategoryDefinition?> GetByIdAsync(
        string categoryId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryId);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(definitions.GetValueOrDefault(categoryId.Trim()));
    }
}
