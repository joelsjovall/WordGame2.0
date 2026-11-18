using WordGame.Application.Games;
using WordGame.Domain;

namespace WordGame.Infrastructure.Tests;

public sealed class GameEngineSessionTests
{
    [Fact]
    public async Task CompetingPlayerActionsCommitOnlyOneTurnChangeInTheRealStore()
    {
        var clock = new ManualSessionClock();
        var store = SessionTestData.Store(clock);
        var engine = Engine(store, clock);
        var players = SessionTestData.Players();
        var id = (await engine.CreateAsync(players)).State!.Id;
        Assert.True((await engine.InitializeAsync(id)).Succeeded);
        Assert.True((await engine.StartAsync(id)).Succeeded);
        Assert.True((await engine.SelectCategoryAsync(id, players[0].Id,
            "cars", new(Guid.NewGuid(), (await store.GetAsync(id))!.Version))).Succeeded);
        var before = await store.GetAsync(id);

        var results = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(_ => Task.Run(() => engine.PlaceBidAsync(id, players[0].Id, 1, new(Guid.NewGuid(), before!.Version)))));

        Assert.Single(results, result => result.Succeeded);
        Assert.All(results.Where(result => !result.Succeeded), result => Assert.Equal(GameEngineErrorCode.StaleState, result.Error!.Code));
        var after = await store.GetAsync(id);
        Assert.Equal(before!.Version + 1, after!.Version);
        Assert.Equal(2, after.Game.CurrentRound!.Turn.Number);
        Assert.Equal(players[1].Id, after.Game.CurrentRound.Turn.PlayerId);
        Assert.Equal(1, after.Game.CurrentRound.HighestBid!.WordCount);
    }

    [Fact]
    public async Task MultipleEngineInstancesShareAuthoritativeSessionsWithoutCrossGameChanges()
    {
        var clock = new ManualSessionClock();
        var store = SessionTestData.Store(clock);
        var firstEngine = Engine(store, clock);
        var secondEngine = Engine(store, clock);
        var players = SessionTestData.Players(4);
        var firstId = (await firstEngine.CreateAsync(players)).State!.Id;
        var secondId = (await secondEngine.CreateAsync(players)).State!.Id;
        await Task.WhenAll(firstEngine.InitializeAsync(firstId), secondEngine.InitializeAsync(secondId));
        await Task.WhenAll(firstEngine.StartAsync(firstId), secondEngine.StartAsync(secondId));
        var before = await store.GetAsync(secondId);

        Assert.True((await secondEngine.CancelAsync(firstId)).Succeeded);

        Assert.Equal(GameStatus.Cancelled, (await firstEngine.GetStateAsync(firstId)).State!.Game.Status);
        var unchanged = (await firstEngine.GetStateAsync(secondId)).State!;
        Assert.Equal(before!.Version, unchanged.Version);
        Assert.Equal(GameStatus.InProgress, unchanged.Game.Status);
        Assert.Equal(players[0].Id, unchanged.Game.CurrentRound!.Turn.PlayerId);
    }

    private static GameEngine Engine(WordGame.Application.Sessions.IGameSessionStore store, TimeProvider clock) =>
        SessionTestData.Engine(store, clock);
}
