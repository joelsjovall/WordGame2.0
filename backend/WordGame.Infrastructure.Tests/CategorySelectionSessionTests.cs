using WordGame.Application.Categories;
using WordGame.Application.Games;
using WordGame.Application.Sessions;
using WordGame.Domain;
using WordGame.Infrastructure.Categories;

namespace WordGame.Infrastructure.Tests;

public sealed class CategorySelectionSessionTests
{
    [Fact]
    public async Task ConcurrentSelectionsPinExactlyOneConfiguredJsonDefinition()
    {
        using var directory = Dataset();
        var provider = new JsonCategoryProvider(directory.Root);
        var clock = new ManualSessionClock();
        var store = SessionTestData.Store(clock);
        var retention = Retention();
        var categories = new CategorySelectionService(provider, new CategorySelectionRules(null), store, retention);
        var engine = new GameEngine(store, clock, retention, categories);
        var players = SessionTestData.Players();
        var id = (await engine.CreateAsync(players)).State!.Id;
        Assert.Empty(await store.GetActiveAsync());
        Assert.True((await engine.InitializeAsync(id)).Succeeded);
        Assert.True((await engine.StartAsync(id)).Succeeded);
        var before = (await store.GetAsync(id))!;
        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(() =>
            engine.SelectCategoryAsync(id, players[0].Id, "cars", new(Guid.NewGuid(), before.Version)))));
        Assert.Single(results, result => result.Succeeded);
        var selected = (await store.GetAsync(id))!;
        Assert.Equal(before.Version + 1, selected.Version);
        Assert.Same(await provider.GetByIdAsync("cars"), selected.SelectedCategory!.Definition);
        var active = Assert.Single(await store.GetActiveAsync());
        active.Game.Cancel();
        Assert.Equal(GameStatus.InProgress, (await store.GetAsync(id))!.Game.Status);
    }

    [Fact]
    public async Task CompetingTimeoutsCommitOneSelectionAndOneRevision()
    {
        using var directory = Dataset();
        var clock = new ManualSessionClock();
        var store = SessionTestData.Store(clock);
        var retention = Retention();
        var categories = new CategorySelectionService(new JsonCategoryProvider(directory.Root),
            new CategorySelectionRules(TimeSpan.FromSeconds(45)), store, retention);
        var engine = new GameEngine(store, clock, retention, categories);
        var id = (await engine.CreateAsync(SessionTestData.Players())).State!.Id;
        Assert.True((await engine.InitializeAsync(id)).Succeeded);
        Assert.True((await engine.StartAsync(id)).Succeeded);
        var version = (await store.GetAsync(id))!.Version;
        clock.Advance(TimeSpan.FromSeconds(45));
        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(() => categories.ExpireAsync(id, version))));
        Assert.Single(results, result => result.Succeeded);
        var after = (await store.GetAsync(id))!;
        Assert.Equal(version + 1, after.Version);
        Assert.Equal(RoundStatus.Bidding, after.Game.CurrentRound!.Status);
        Assert.Equal(1, after.Game.CurrentRound.Turn.Number);
        Assert.NotNull(after.SelectedCategory);
    }

    [Fact]
    public async Task ProviderAwaitDoesNotHoldSessionLockAndLateResultsCannotOverwriteNewState()
    {
        using var directory = Dataset();
        var provider = new PausedProvider(new JsonCategoryProvider(directory.Root));
        var clock = new ManualSessionClock();
        var store = SessionTestData.Store(clock);
        var retention = Retention();
        var categories = new CategorySelectionService(provider, new CategorySelectionRules(null), store, retention);
        var engine = new GameEngine(store, clock, retention, categories);
        var players = SessionTestData.Players(3);
        var id = (await engine.CreateAsync(players)).State!.Id;
        Assert.True((await engine.InitializeAsync(id)).Succeeded);
        Assert.True((await engine.StartAsync(id)).Succeeded);
        var before = (await store.GetAsync(id))!;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var pending = engine.SelectCategoryAsync(id, players[0].Id, "cars", new(Guid.NewGuid(), before.Version), timeout.Token);
        try
        {
            await provider.Entered.Task.WaitAsync(timeout.Token);
            var leave = await engine.LeaveAsync(id, players[0].Id, new(Guid.NewGuid(), before.Version), timeout.Token).WaitAsync(timeout.Token);
            Assert.True(leave.Succeeded);
        }
        finally
        {
            provider.Release.TrySetResult();
        }
        Assert.Equal(GameEngineErrorCode.StaleState, (await pending).Error!.Code);
        var after = (await store.GetAsync(id))!;
        Assert.Null(after.SelectedCategory);
        Assert.Equal(players[1].Id, after.Game.CurrentRound!.Turn.PlayerId);
    }

    private static TemporaryCategoryDirectory Dataset()
    {
        var directory = new TemporaryCategoryDirectory();
        directory.Write("cars.json", """{"id":"cars","name":"Cars","difficulty":"easy","answers":[{"value":"Volvo","aliases":[]}]}""");
        return directory;
    }

    private static SessionRetention Retention() => new(TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(5));

    private sealed class PausedProvider(ICategoryProvider inner) : ICategoryProvider
    {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<IReadOnlyList<Category>> GetCategoriesAsync(CategoryDifficulty? difficulty = null, CancellationToken cancellationToken = default) =>
            inner.GetCategoriesAsync(difficulty, cancellationToken);
        public async Task<CategoryDefinition?> GetByIdAsync(string categoryId, CancellationToken cancellationToken = default)
        {
            Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return await inner.GetByIdAsync(categoryId, cancellationToken);
        }
    }
}
