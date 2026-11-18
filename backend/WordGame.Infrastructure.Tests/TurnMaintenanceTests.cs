using WordGame.Application.Games;
using WordGame.Application.Sessions;
using WordGame.Domain;

namespace WordGame.Infrastructure.Tests;

public sealed class TurnMaintenanceTests
{
    [Fact]
    public async Task PeriodicSweepAdvancesAnExpiredTurnOnceAndPublishesTheSharedRevision()
    {
        var clock = new ManualSessionClock();
        var store = SessionTestData.Store(clock);
        var engine = SessionTestData.Engine(store, clock);
        var players = SessionTestData.Players(3);
        var id = (await engine.CreateAsync(players)).State!.Id;
        var connections = players.Select(_ => Guid.NewGuid()).ToArray();
        for (var index = 0; index < players.Length; index++)
        {
            Assert.True((await engine.ConnectAsync(id, players[index].Id, connections[index])).Succeeded);
        }

        Assert.True((await engine.InitializeAsync(id)).Succeeded);
        Assert.True((await engine.StartAsync(id)).Succeeded);
        Assert.True((await engine.DisconnectAsync(id, players[0].Id, connections[0])).Succeeded);
        var before = (await store.GetAsync(id))!;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var changes = store.WatchAsync(id, timeout.Token).GetAsyncEnumerator();
        Assert.True(await changes.MoveNextAsync());
        clock.Advance(TimeSpan.FromMinutes(2) - TimeSpan.FromTicks(1));
        await store.CleanupAsync();
        Assert.Equal(before.Version, (await store.GetAsync(id))!.Version);
        clock.Advance(TimeSpan.FromTicks(1));

        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(() => store.CleanupAsync())));

        var after = (await store.GetAsync(id))!;
        Assert.Equal(before.Version + 1, after.Version);
        Assert.Equal(players[1].Id, after.Game.CurrentRound!.Turn.PlayerId);
        Assert.Equal(2, after.Game.CurrentRound.Turn.Number);
        Assert.True(await changes.MoveNextAsync());
        Assert.Equal(after.Version, changes.Current.Version);
        Assert.Equal(SessionStatus.Active, changes.Current.Status);
    }

    [Fact]
    public async Task RevisionConflictNeverInvokesAnUpdateCallback()
    {
        var store = SessionTestData.Store(new ManualSessionClock());
        var created = await store.CreateAsync(SessionTestData.Players());
        await store.UpdateAsync(created.Id, session => session.BeginStarting());
        var invoked = false;
        await Assert.ThrowsAsync<SessionVersionConflictException>(() =>
            store.UpdateAsync(created.Id, created.Version, _ => invoked = true));
        Assert.False(invoked);
        Assert.Equal(1, (await store.GetAsync(created.Id))!.Version);
    }

    [Fact]
    public async Task ConcurrentReplayCommitsOnceAndTheActionGuardSurvivesStoreCopies()
    {
        var clock = new ManualSessionClock();
        var store = SessionTestData.Store(clock);
        var engine = SessionTestData.Engine(store, clock);
        var players = SessionTestData.Players(2);
        var id = (await engine.CreateAsync(players)).State!.Id;
        Assert.True((await engine.InitializeAsync(id)).Succeeded);
        Assert.True((await engine.StartAsync(id)).Succeeded);
        var context = new PlayerActionContext(Guid.NewGuid(), (await store.GetAsync(id))!.Version);
        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(() =>
            engine.SelectCategoryAsync(id, players[0].Id, "cars", context))));
        Assert.Single(results, result => result.Succeeded);
        var version = (await store.GetAsync(id))!.Version;
        var replay = await engine.PlaceBidAsync(id, players[0].Id, 1, context with { ExpectedVersion = version });
        Assert.Equal(GameEngineErrorCode.DuplicateAction, replay.Error!.Code);
        Assert.Equal(version, (await store.GetAsync(id))!.Version);
    }

    [Fact]
    public async Task SweepCancelsActiveOneVersusOneWhenTheOpponentDoesNotReturn()
    {
        var clock = new ManualSessionClock();
        var store = SessionTestData.Store(clock);
        var engine = SessionTestData.Engine(store, clock);
        var players = SessionTestData.Players(2);
        var id = (await engine.CreateAsync(players)).State!.Id;
        Assert.True((await engine.ConnectAsync(id, players[0].Id, Guid.NewGuid())).Succeeded);
        Assert.True((await engine.InitializeAsync(id)).Succeeded);
        Assert.True((await engine.StartAsync(id)).Succeeded);
        clock.Advance(TimeSpan.FromMinutes(2));
        await store.CleanupAsync();
        var state = (await store.GetAsync(id))!;
        Assert.Equal(SessionStatus.Abandoned, state.Status);
        Assert.Equal(GameStatus.Cancelled, state.Game.Status);
        Assert.All(state.Game.Scores.Values, score => Assert.Equal(0, score.Points));
    }


}
