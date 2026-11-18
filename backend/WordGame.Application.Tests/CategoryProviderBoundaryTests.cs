using WordGame.Application.Categories;
using WordGame.Application.Words;
using WordGame.Domain;

namespace WordGame.Application.Tests;

public sealed class CategoryProviderBoundaryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DifferentRetrievalImplementationsUseTheSameValidationService(bool useDelegate)
    {
        var definition = new CategoryDefinition(
            new Category("capitals", "Capitals", CategoryDifficulty.Easy),
            [new AcceptedAnswer("Copenhagen", ["Köpenhamn"])]);
        ICategoryProvider provider = useDelegate
            ? new DelegateProvider((id, _) => Task.FromResult(id == "capitals" ? definition : null))
            : new MemoryProvider(definition);

        var loaded = await provider.GetByIdAsync("capitals");
        Assert.NotNull(loaded);
        var service = new WordValidationService();
        var result = service.Validate(loaded, "KÖPENHAMN", []);

        Assert.Equal(WordSubmissionStatus.Accepted, result.Status);
        Assert.Equal("copenhagen", result.CanonicalAnswerId);
        Assert.Null(await provider.GetByIdAsync("missing"));
    }

    private sealed class MemoryProvider(CategoryDefinition definition) : ICategoryProvider
    {
        public Task<CategoryDefinition?> GetByIdAsync(string categoryId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(categoryId == definition.Category.Id ? definition : null);
        }

        public Task<IReadOnlyList<Category>> GetCategoriesAsync(
            CategoryDifficulty? difficulty = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<Category> categories = difficulty is null || difficulty == definition.Category.Difficulty
                ? new[] { definition.Category }
                : [];
            return Task.FromResult(categories);
        }
    }

    private sealed class DelegateProvider(
        Func<string, CancellationToken, Task<CategoryDefinition?>> load) : ICategoryProvider
    {
        public Task<CategoryDefinition?> GetByIdAsync(string categoryId, CancellationToken cancellationToken = default) =>
            load(categoryId, cancellationToken);

        public Task<IReadOnlyList<Category>> GetCategoriesAsync(
            CategoryDifficulty? difficulty = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Catalog listing is not used by this retrieval boundary test.");
    }
}
