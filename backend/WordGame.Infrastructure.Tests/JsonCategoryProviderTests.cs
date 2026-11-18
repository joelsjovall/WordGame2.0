using WordGame.Application.Categories;
using WordGame.Application.Words;
using WordGame.Domain;
using WordGame.Infrastructure.Categories;

namespace WordGame.Infrastructure.Tests;

public sealed class JsonCategoryProviderTests
{
    private const string ValidJson = """
        {"id":"capitals","name":"Capitals","difficulty":"easy","answers":[{"value":"Copenhagen","aliases":["Köpenhamn"]}]}
        """;

    [Fact]
    public async Task JsonMetadataAnswersAndAliasesDriveRealValidationWithoutADatabase()
    {
        using var directory = new TemporaryCategoryDirectory();
        directory.Write("capitals.json", ValidJson);
        ICategoryProvider provider = new JsonCategoryProvider(directory.Root);

        var loaded = await provider.GetByIdAsync("capitals");

        Assert.NotNull(loaded);
        Assert.Equal("Capitals", loaded.Category.Name);
        Assert.Equal(CategoryDifficulty.Easy, loaded.Category.Difficulty);
        Assert.Equal("Copenhagen", Assert.Single(loaded.Answers).Value);
        Assert.Equal("Köpenhamn", Assert.Single(loaded.Answers[0].Aliases));
        var result = new WordValidationService().Validate(loaded, " KÖPENHAMN ", []);
        Assert.Equal(WordSubmissionStatus.Accepted, result.Status);
        Assert.Equal("copenhagen", result.CanonicalAnswerId);
    }

    [Fact]
    public async Task ExplicitStableAnswerIdsArePreserved()
    {
        using var directory = new TemporaryCategoryDirectory();
        directory.Write("capitals.json", ValidJson.Replace("\"value\":", "\"id\":\"Capital:CPH\",\"value\":"));
        var category = await new JsonCategoryProvider(directory.Root).GetByIdAsync("capitals");
        Assert.Equal("Capital:CPH", category!.FindAnswer("Köpenhamn")!.Id);
    }

    [Fact]
    public async Task CatalogIsSortedFilteredAndReadOnly()
    {
        using var directory = new TemporaryCategoryDirectory();
        directory.Write("z.json", ValidJson.Replace("capitals", "z-category").Replace("easy", "hard"));
        directory.Write("m.json", ValidJson.Replace("capitals", "m-category").Replace("easy", "medium"));
        directory.Write("a.json", ValidJson.Replace("capitals", "a-category"));
        var provider = new JsonCategoryProvider(directory.Root);

        var catalog = await provider.GetCategoriesAsync();
        Assert.Equal(new[] { "a-category", "m-category", "z-category" }, catalog.Select(category => category.Id));
        Assert.Equal("a-category", Assert.Single(await provider.GetCategoriesAsync(CategoryDifficulty.Easy)).Id);
        Assert.Equal("m-category", Assert.Single(await provider.GetCategoriesAsync(CategoryDifficulty.Medium)).Id);
        Assert.Equal("z-category", Assert.Single(await provider.GetCategoriesAsync(CategoryDifficulty.Hard)).Id);
        Assert.Throws<NotSupportedException>(() => ((IList<Category>)catalog).Clear());
    }

    [Fact]
    public async Task UnknownCategoryReturnsNullAndIdLookupUsesTrimmedOrdinalIdentity()
    {
        using var directory = new TemporaryCategoryDirectory();
        directory.Write("capitals.json", ValidJson);
        var provider = new JsonCategoryProvider(directory.Root);

        Assert.NotNull(await provider.GetByIdAsync(" capitals "));
        Assert.Null(await provider.GetByIdAsync("CAPITALS"));
        Assert.Null(await provider.GetByIdAsync("missing"));
        Assert.Empty(await provider.GetCategoriesAsync(CategoryDifficulty.Hard));
    }

    [Fact]
    public async Task ExistingSnapshotIsReusedAfterFilesChangeOrDisappear()
    {
        using var directory = new TemporaryCategoryDirectory();
        var path = directory.Write("capitals.json", ValidJson);
        var provider = new JsonCategoryProvider(directory.Root);
        var original = await provider.GetByIdAsync("capitals");
        var catalog = await provider.GetCategoriesAsync();
        var filtered = await provider.GetCategoriesAsync(CategoryDifficulty.Easy);

        File.WriteAllText(path, "{ invalid");
        Assert.Throws<InvalidDataException>(() => new JsonCategoryProvider(directory.Root));
        File.Delete(path);

        var concurrent = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => provider.GetByIdAsync("capitals")));
        Assert.All(concurrent, snapshot => Assert.Same(original, snapshot));
        Assert.Same(catalog, await provider.GetCategoriesAsync());
        Assert.Same(filtered, await provider.GetCategoriesAsync(CategoryDifficulty.Easy));
    }

    [Fact]
    public void DuplicateTrimmedCategoryIdsAreRejectedAcrossFiles()
    {
        using var directory = new TemporaryCategoryDirectory();
        directory.Write("first.json", ValidJson);
        directory.Write("second.json", ValidJson.Replace("\"id\":\"capitals\"", "\"id\":\" capitals \""));

        var exception = Assert.Throws<InvalidDataException>(() => new JsonCategoryProvider(directory.Root));
        Assert.Contains("Duplicate category ID 'capitals'", exception.Message);
        Assert.Contains("second.json", exception.Message);
    }

    [Fact]
    public void EmptyAndMissingDirectoriesFailInsteadOfReturningAnEmptyCatalog()
    {
        using var directory = new TemporaryCategoryDirectory();
        Assert.Throws<InvalidDataException>(() => new JsonCategoryProvider(directory.Root));
        Assert.Throws<DirectoryNotFoundException>(() => new JsonCategoryProvider(Path.Combine(directory.Root, "missing")));
    }

    [Fact]
    public async Task ProviderRejectsInvalidArgumentsAndHonorsCancellationOnCachedReads()
    {
        using var directory = new TemporaryCategoryDirectory();
        directory.Write("capitals.json", ValidJson);
        var provider = new JsonCategoryProvider(directory.Root);
        Assert.ThrowsAny<ArgumentException>(() => new JsonCategoryProvider(" "));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => provider.GetByIdAsync(" "));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => provider.GetByIdAsync(null!));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => provider.GetCategoriesAsync((CategoryDifficulty)99));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.GetByIdAsync("capitals", cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.GetCategoriesAsync(cancellationToken: cancellation.Token));
        Assert.NotNull(await provider.GetByIdAsync("capitals"));
    }

    [Theory]
    [MemberData(nameof(InvalidDocuments))]
    public void InvalidDataReportsTheFileAndCauseWithoutReturningPartialResults(string json, string message)
    {
        using var directory = new TemporaryCategoryDirectory();
        directory.Write("a-valid.json", ValidJson);
        directory.Write("bad.json", json);

        var exception = Assert.Throws<InvalidDataException>(() => new JsonCategoryProvider(directory.Root));

        Assert.Contains("bad.json", exception.Message);
        Assert.Contains(message, exception.Message);
        Assert.NotNull(exception.InnerException);
    }

    public static TheoryData<string, string> InvalidDocuments => new()
    {
        { "{", "Cannot load category file" },
        { "null", "objects" },
        { "[]", "objects" },
        { ValidJson.Replace("\"name\":\"Capitals\",", ""), "name" },
        { ValidJson.Replace("\"id\":\"capitals\"", "\"id\":null"), "id" },
        { ValidJson.Replace("\"name\":\"Capitals\"", "\"name\":\" \""), "name" },
        { ValidJson.Replace("\"difficulty\":\"easy\"", "\"difficulty\":42"), "difficulty" },
        { ValidJson.Replace("\"easy\"", "\"expert\""), "difficulty" },
        { ValidJson.Replace("\"easy\"", "\"Easy\""), "difficulty" },
        { ValidJson.Replace("\"answers\":[{\"value\":\"Copenhagen\",\"aliases\":[\"Köpenhamn\"]}]", "\"answers\":[]"), "at least one" },
        { ValidJson.Replace("[{\"value\":\"Copenhagen\",\"aliases\":[\"Köpenhamn\"]}]", "[null]"), "objects" },
        { ValidJson.Replace("\"aliases\":[\"Köpenhamn\"]", "\"aliases\":null"), "aliases" },
        { ValidJson.Replace("[\"Köpenhamn\"]", "[null]"), "aliases" },
        { ValidJson.Replace("[\"Köpenhamn\"]", "[\" \"]"), "aliases" },
        { ValidJson.Replace("\"value\":\"Copenhagen\"", "\"value\":0"), "value" },
        { ValidJson.Replace("\"value\":", "\"id\":null,\"value\":"), "id" },
        { ValidJson.Replace("\"name\":", "\"extra\":true,\"name\":"), "Unknown property 'extra'" },
        { ValidJson.Replace("\"name\":", "\"id\":\"other\",\"name\":"), "Duplicate JSON property 'id'" },
        { ValidJson.Replace("\"value\":\"Copenhagen\"", "\"value\":\"Copenhagen\",\"value\":\"Oslo\""), "Duplicate JSON property 'value'" },
        { """{"id":"x","name":"X","difficulty":"easy","answers":[{"value":"A","aliases":[],"id":"same"},{"value":"B","aliases":[],"id":"same"}]}""", "unique" },
        { """{"id":"x","name":"X","difficulty":"easy","answers":[{"value":"A","aliases":["B"]},{"value":"B","aliases":[]}]}""", "ambiguous" }
    };
}
