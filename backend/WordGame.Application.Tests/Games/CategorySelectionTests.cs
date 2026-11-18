using WordGame.Application.Categories;
using WordGame.Application.Games;
using WordGame.Domain;

namespace WordGame.Application.Tests.Games;

public sealed class CategorySelectionTests
{
    [Theory]
    [InlineData(2, CategoryDifficulty.Easy)]
    [InlineData(3, CategoryDifficulty.Medium)]
    [InlineData(4, CategoryDifficulty.Hard)]
    public async Task BothModesSelectProviderDefinitionsWithConsistentDifficulty(int count, CategoryDifficulty difficulty)
    {
        var definition = Definition("new-category", difficulty);
        var provider = new TestCategoryProvider(definition);
        var multipliers = new Dictionary<CategoryDifficulty, int>
        {
            [CategoryDifficulty.Easy] = 1,
            [CategoryDifficulty.Medium] = 2,
            [CategoryDifficulty.Hard] = 3
        };
        var rules = new CategorySelectionRules(TimeSpan.FromSeconds(45), scoreMultipliers: multipliers);
        var game = await TurnTestGame.Create(count, provider, rules);
        var available = await game.Categories.GetAvailableAsync(difficulty);
        Assert.Equal(definition.Category, Assert.Single(available));
        var selected = TurnTestGame.Accepted(await game.Engine.SelectCategoryAsync(game.Id, game.Players[0].Id, " new-category ", await game.Context()));
        Assert.Equal(definition.Category, selected.Game.CurrentRound!.Category);
        Assert.Same(definition, selected.SelectedCategory!.Definition);
        Assert.Equal(selected.Game.CurrentRound.Id, selected.SelectedCategory.RoundId);
        Assert.Equal(multipliers[difficulty], selected.SelectedCategory.ScoreMultiplier);
        Assert.Equal(RoundStatus.Bidding, selected.Game.CurrentRound.Status);
        Assert.Null(selected.CategorySelectionDeadline);
        Assert.All(selected.Game.Scores.Values, score => Assert.Equal(0, score.Points));
    }

    [Fact]
    public async Task UnauthorizedSelectionCannotResetTheDeadlineOrReplaceALockedCategory()
    {
        var game = await TurnTestGame.Create(rules: new CategorySelectionRules(TimeSpan.FromSeconds(45)));
        var before = await game.State();
        game.Clock.Advance(TimeSpan.FromSeconds(5));
        Assert.Equal(GameEngineErrorCode.PlayerNotInGame,
            (await game.Engine.SelectCategoryAsync(game.Id, Guid.NewGuid(), "cars", await game.Context())).Error!.Code);
        Assert.Equal(GameEngineErrorCode.NotYourTurn,
            (await game.Engine.SelectCategoryAsync(game.Id, game.Players[1].Id, "cars", await game.Context())).Error!.Code);
        Assert.Equal(before.Version, (await game.State()).Version);
        Assert.Equal(before.CategorySelectionDeadline, (await game.State()).CategorySelectionDeadline);
        var selected = await game.Select();
        Assert.Equal(GameEngineErrorCode.InvalidState,
            (await game.Engine.SelectCategoryAsync(game.Id, game.Players[0].Id, "cars", await game.Context())).Error!.Code);
        Assert.Equal(selected.Version, (await game.State()).Version);
        Assert.Equal(selected.SelectedCategory, (await game.State()).SelectedCategory);
    }

    [Fact]
    public async Task UnknownDisabledAndUnlistedCategoriesCannotChangeState()
    {
        var provider = new TestCategoryProvider(Definition("easy", CategoryDifficulty.Easy), Definition("hard", CategoryDifficulty.Hard));
        var game = await TurnTestGame.Create(provider: provider,
            rules: new CategorySelectionRules(null, [CategoryDifficulty.Easy]));
        var before = await game.State();
        foreach (var id in new[] { "unknown", "hard" })
        {
            Assert.Equal(GameEngineErrorCode.CategoryUnavailable,
                (await game.Engine.SelectCategoryAsync(game.Id, game.Players[0].Id, id, await game.Context())).Error!.Code);
        }

        provider.CatalogOverride = [];
        Assert.Equal(GameEngineErrorCode.CategoryUnavailable,
            (await game.Engine.SelectCategoryAsync(game.Id, game.Players[0].Id, "easy", await game.Context())).Error!.Code);
        Assert.Equal(before.Version, (await game.State()).Version);
        Assert.Null((await game.State()).Game.CurrentRound!.Category);
    }

    [Fact]
    public async Task SelectionRetainsAnswersAndModifierWhenProviderDataChanges()
    {
        var original = Definition("cars", CategoryDifficulty.Easy, "Volvo");
        var provider = new TestCategoryProvider(original);
        var game = await TurnTestGame.Create(provider: provider);
        await game.Select();
        provider.Definitions["cars"] = Definition("cars", CategoryDifficulty.Hard, "Saab");
        var retained = (await game.State()).SelectedCategory!;
        Assert.Same(original, retained.Definition);
        Assert.NotNull(retained.Definition.FindAnswer("Volvo"));
        Assert.Null(retained.Definition.FindAnswer("Saab"));
        Assert.Equal(CategoryDifficulty.Easy, retained.Definition.Category.Difficulty);
        Assert.Equal(1, retained.ScoreMultiplier);
        var locked = await game.Engine.SelectCategoryAsync(game.Id, game.Players[0].Id, "cars", await game.Context());
        Assert.Equal(GameEngineErrorCode.InvalidState, locked.Error!.Code);
        Assert.Same(original, (await game.State()).SelectedCategory!.Definition);
    }

    [Fact]
    public async Task NewRoundsClearTheOldSnapshotAndStartAFreshSelectionWindow()
    {
        var game = await TurnTestGame.Create(rules: new CategorySelectionRules(TimeSpan.FromSeconds(45)));
        await game.Select();
        await game.Bid(0, 1);
        await game.Bluff(1);
        game.Clock.Advance(TurnTestGame.Duration);
        TurnTestGame.Accepted(await game.Engine.ResolveChallengeAsync(game.Id));
        var next = TurnTestGame.Accepted(await game.Engine.NextRoundAsync(game.Id));
        Assert.Null(next.SelectedCategory);
        Assert.Equal(game.Clock.GetUtcNow().AddSeconds(45), next.CategorySelectionDeadline);
        Assert.Null(next.Game.CurrentRound!.Category);
    }

    [Fact]
    public async Task ProviderFailuresAndMismatchedDefinitionsAreNotUnknownCategories()
    {
        var provider = new TestCategoryProvider();
        var game = await TurnTestGame.Create(provider: provider);
        var version = (await game.State()).Version;
        provider.LoadOverride = (_, _) => throw new IOException("Simulated provider failure.");
        await Assert.ThrowsAsync<IOException>(async () =>
            await game.Engine.SelectCategoryAsync(game.Id, game.Players[0].Id, "cars", await game.Context()));
        provider.LoadOverride = (_, _) => Task.FromResult<CategoryDefinition?>(Definition("wrong-id", CategoryDifficulty.Easy));
        await Assert.ThrowsAsync<InvalidDataException>(async () =>
            await game.Engine.SelectCategoryAsync(game.Id, game.Players[0].Id, "cars", await game.Context()));
        Assert.Equal(version, (await game.State()).Version);
    }

    [Fact]
    public async Task RulesCopyInputsAndRejectInvalidConfiguration()
    {
        var allowed = new[] { CategoryDifficulty.Easy };
        var multipliers = Enum.GetValues<CategoryDifficulty>().ToDictionary(difficulty => difficulty, _ => 1);
        var rules = new CategorySelectionRules(null, allowed, multipliers);
        allowed[0] = CategoryDifficulty.Hard;
        multipliers[CategoryDifficulty.Easy] = 9;
        Assert.True(rules.AllowedDifficulties.SetEquals([CategoryDifficulty.Easy]));
        Assert.Equal(1, rules.ScoreMultipliers[CategoryDifficulty.Easy]);
        Assert.Throws<ArgumentOutOfRangeException>(() => new CategorySelectionRules(TimeSpan.Zero));
        Assert.Throws<ArgumentException>(() => new CategorySelectionRules(null, []));
        Assert.Throws<ArgumentException>(() => new CategorySelectionRules(null, [(CategoryDifficulty)99]));
        Assert.Throws<ArgumentException>(() => new CategorySelectionRules(null, scoreMultipliers: new Dictionary<CategoryDifficulty, int>()));
        multipliers[CategoryDifficulty.Easy] = 0;
        Assert.Throws<ArgumentException>(() => new CategorySelectionRules(null, scoreMultipliers: multipliers));
        var game = await TurnTestGame.Create();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => game.Categories.GetAvailableAsync((CategoryDifficulty)99));
    }

    internal static CategoryDefinition Definition(string id, CategoryDifficulty difficulty, string answer = "Example") =>
        new(new Category(id, id, difficulty), [new AcceptedAnswer(answer)]);
}
