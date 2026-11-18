using WordGame.Application.Categories;
using WordGame.Application.Games;
using WordGame.Application.Sessions;
using WordGame.Domain;

namespace WordGame.Application.Tests.Games;

public sealed class CategoryTimeoutTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(45);

    [Fact]
    public async Task ExactDeadlineRejectsManualSelectionAndAutomaticallyChoosesSortedAvailableCategory()
    {
        var provider = new TestCategoryProvider(CategorySelectionTests.Definition("z-last", CategoryDifficulty.Easy),
            CategorySelectionTests.Definition("a-first", CategoryDifficulty.Hard));
        var game = await TurnTestGame.Create(provider: provider, rules: new CategorySelectionRules(Timeout));
        var before = await game.State();
        Assert.Equal(game.Clock.GetUtcNow() + Timeout, before.CategorySelectionDeadline);
        Assert.Equal(GameEngineErrorCode.InvalidState,
            (await game.Categories.ExpireAsync(game.Id, before.Version)).Error!.Code);
        game.Clock.Advance(Timeout);
        Assert.Equal(GameEngineErrorCode.InvalidState,
            (await game.Engine.SelectCategoryAsync(game.Id, game.Players[0].Id, "z-last", await game.Context())).Error!.Code);
        Assert.Equal(before.Version, (await game.State()).Version);
        var expired = TurnTestGame.Accepted(await game.Categories.ExpireAsync(game.Id, before.Version));
        Assert.Equal("a-first", expired.Game.CurrentRound!.Category!.Id);
        Assert.NotNull(expired.SelectedCategory);
        Assert.Null(expired.CategorySelectionDeadline);
        Assert.Equal(before.Version + 1, expired.Version);
        Assert.Equal(GameEngineErrorCode.InvalidState,
            (await game.Categories.ExpireAsync(game.Id, expired.Version)).Error!.Code);
    }

    [Fact]
    public async Task SelectionJustBeforeDeadlineSucceedsAndAnOldTimeoutCannotReplaceIt()
    {
        var game = await TurnTestGame.Create(rules: new CategorySelectionRules(Timeout));
        var old = await game.State();
        game.Clock.Advance(Timeout - TimeSpan.FromTicks(1));
        var selected = await game.Select();
        game.Clock.Advance(TimeSpan.FromTicks(1));
        Assert.Equal(GameEngineErrorCode.StaleState,
            (await game.Categories.ExpireAsync(game.Id, old.Version)).Error!.Code);
        Assert.Equal(selected.Version, (await game.State()).Version);
        Assert.Equal(selected.SelectedCategory, (await game.State()).SelectedCategory);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EmptyCatalogOrConfiguredCancellationEndsTheMatchSafely(bool cancel)
    {
        var provider = new TestCategoryProvider();
        var game = await TurnTestGame.Create(provider: provider, rules: new CategorySelectionRules(Timeout,
            timeoutBehavior: cancel ? CategoryTimeoutBehavior.CancelMatch : CategoryTimeoutBehavior.AutoSelect));
        if (!cancel)
        {
            provider.Definitions.Clear();
        }

        game.Clock.Advance(Timeout);
        var result = TurnTestGame.Accepted(await game.Categories.ExpireAsync(game.Id, (await game.State()).Version));
        Assert.Equal(SessionStatus.Abandoned, result.Status);
        Assert.Equal(GameStatus.Cancelled, result.Game.Status);
        Assert.Null(result.CategorySelectionDeadline);
        Assert.Null(result.SelectedCategory);
    }

    [Fact]
    public async Task ProviderFailureLeavesExpiredSelectionRetryable()
    {
        var provider = new TestCategoryProvider();
        var game = await TurnTestGame.Create(provider: provider, rules: new CategorySelectionRules(Timeout));
        var version = (await game.State()).Version;
        game.Clock.Advance(Timeout);
        provider.LoadOverride = (_, _) => throw new IOException("Simulated provider failure.");
        await Assert.ThrowsAsync<IOException>(() => game.Categories.ExpireAsync(game.Id, version));
        Assert.Equal(version, (await game.State()).Version);
        provider.LoadOverride = null;
        Assert.Equal(RoundStatus.Bidding, TurnTestGame.Accepted(await game.Categories.ExpireAsync(game.Id, version)).Game.CurrentRound!.Status);
    }

    [Fact]
    public async Task DisabledTimeoutKeepsSelectionOpenAndRequestCancellationPropagates()
    {
        var game = await TurnTestGame.Create(rules: new CategorySelectionRules(null));
        Assert.Null((await game.State()).CategorySelectionDeadline);
        Assert.Equal(GameEngineErrorCode.InvalidState,
            (await game.Categories.ExpireAsync(game.Id, (await game.State()).Version)).Error!.Code);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await game.Engine.SelectCategoryAsync(game.Id, game.Players[0].Id, "cars", await game.Context(), cancellation.Token));
    }
}
