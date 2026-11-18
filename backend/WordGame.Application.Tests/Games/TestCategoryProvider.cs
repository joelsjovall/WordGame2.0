using WordGame.Application.Categories;
using WordGame.Application.Challenges;
using WordGame.Application.Games;
using WordGame.Application.Sessions;
using WordGame.Application.Words;
using WordGame.Domain;

namespace WordGame.Application.Tests.Games;

internal sealed class TestCategoryProvider(params CategoryDefinition[] definitions) : ICategoryProvider
{
    internal Dictionary<string, CategoryDefinition> Definitions { get; } =
        (definitions.Length > 0 ? definitions : [new(new Category("cars", "Cars", CategoryDifficulty.Easy), [new AcceptedAnswer("Volvo")])])
        .ToDictionary(definition => definition.Category.Id, StringComparer.Ordinal);
    internal Func<string, CancellationToken, Task<CategoryDefinition?>>? LoadOverride { get; set; }
    internal IReadOnlyList<Category>? CatalogOverride { get; set; }

    public Task<IReadOnlyList<Category>> GetCategoriesAsync(CategoryDifficulty? difficulty = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<Category> catalog = (CatalogOverride ?? Definitions.Values.Select(definition => definition.Category).ToArray())
            .Where(category => !difficulty.HasValue || category.Difficulty == difficulty).ToArray();
        return Task.FromResult(catalog);
    }

    public Task<CategoryDefinition?> GetByIdAsync(string categoryId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return LoadOverride is { } load ? load(categoryId, cancellationToken) : Task.FromResult(Definitions.GetValueOrDefault(categoryId));
    }

    internal static GameEngine Engine(IGameSessionStore store, SessionRetention retention) =>
        new(store, retention, new CategorySelectionService(new TestCategoryProvider(), new CategorySelectionRules(null), store, retention),
            new ChallengeService(store, retention, new WordValidationService(), new ChallengeRules(TimeSpan.FromSeconds(30))));
}
