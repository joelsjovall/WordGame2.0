using WordGame.Application.Games;
using WordGame.Application.Sessions;
using WordGame.Domain;

namespace WordGame.Infrastructure.Tests;

public sealed class BiddingConcurrencyTests
{
    [Fact]
    public async Task ACompetingRaiseAndBluffCommitOneConsistentOutcome()
    {
        var (engine, store, id, players) = await Game();
        var before = (await store.GetAsync(id))!;
        var raise = Task.Run(() => engine.PlaceBidAsync(id, players[1].Id, 5, new(Guid.NewGuid(), before.Version)));
        var bluff = Task.Run(() => engine.CallBluffAsync(id, players[1].Id, TimeSpan.FromSeconds(30), new(Guid.NewGuid(), before.Version)));
        var results = await Task.WhenAll(raise, bluff);
        Assert.Single(results, result => result.Succeeded);
        Assert.Equal(GameEngineErrorCode.StaleState, Assert.Single(results, result => !result.Succeeded).Error!.Code);
        var after = (await store.GetAsync(id))!;
        var round = after.Game.CurrentRound!;
        Assert.Equal(before.Version + 1, after.Version);
        Assert.Equal(players[0].Id, round.Turn.PlayerId);
        if (results[0].Succeeded)
        {
            Assert.Equal(RoundStatus.Bidding, round.Status);
            Assert.Equal(new Bid(players[1].Id, 5), round.HighestBid);
            Assert.Null(round.Challenge);
        }
        else
        {
            Assert.Equal(RoundStatus.Challenge, round.Status);
            Assert.Equal(new Bid(players[0].Id, 3), round.HighestBid);
            Assert.Equal(round.HighestBid, round.Challenge!.Bid);
            Assert.Equal(players[1].Id, round.Challenge.ChallengerId);
        }
    }

    [Fact]
    public async Task ConcurrentBluffRetriesCreateExactlyOneChallenge()
    {
        var (engine, store, id, players) = await Game();
        var before = (await store.GetAsync(id))!;
        var context = new PlayerActionContext(Guid.NewGuid(), before.Version);
        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(() =>
            engine.CallBluffAsync(id, players[1].Id, TimeSpan.FromSeconds(30), context))));
        var accepted = Assert.Single(results, result => result.Succeeded).State!;
        Assert.Equal(before.Version + 1, accepted.Version);
        Assert.All(results.Where(result => !result.Succeeded), result => Assert.Equal(GameEngineErrorCode.StaleState, result.Error!.Code));
        var challenge = accepted.Game.CurrentRound!.Challenge!;
        var retry = await engine.CallBluffAsync(id, players[1].Id, TimeSpan.FromSeconds(30), context with { ExpectedVersion = accepted.Version });
        Assert.Equal(GameEngineErrorCode.DuplicateAction, retry.Error!.Code);
        var after = (await store.GetAsync(id))!;
        Assert.Equal(accepted.Version, after.Version);
        Assert.Equal(challenge.Id, after.Game.CurrentRound!.Challenge!.Id);
        Assert.Equal(challenge.Deadline, after.Game.CurrentRound.Challenge.Deadline);
    }

    [Fact]
    public async Task SimultaneousMatchesKeepIndependentHighestBidsAndChallenges()
    {
        var (engine, store, firstId, players) = await Game();
        var secondId = (await engine.CreateAsync(players)).State!.Id;
        Assert.True((await engine.InitializeAsync(secondId)).Succeeded);
        Assert.True((await engine.StartAsync(secondId)).Succeeded);
        Assert.True((await engine.SelectCategoryAsync(secondId, players[0].Id, "cars", new(Guid.NewGuid(), (await store.GetAsync(secondId))!.Version))).Succeeded);
        Assert.True((await engine.PlaceBidAsync(secondId, players[0].Id, 9, new(Guid.NewGuid(), (await store.GetAsync(secondId))!.Version))).Succeeded);
        var firstVersion = (await store.GetAsync(firstId))!.Version;
        var secondVersion = (await store.GetAsync(secondId))!.Version;
        var results = await Task.WhenAll(
            engine.CallBluffAsync(firstId, players[1].Id, TimeSpan.FromSeconds(30), new(Guid.NewGuid(), firstVersion)),
            engine.CallBluffAsync(secondId, players[1].Id, TimeSpan.FromSeconds(30), new(Guid.NewGuid(), secondVersion)));
        Assert.All(results, result => Assert.True(result.Succeeded));
        var first = (await store.GetAsync(firstId))!.Game.CurrentRound!.Challenge!;
        var second = (await store.GetAsync(secondId))!.Game.CurrentRound!.Challenge!;
        Assert.Equal(3, first.Bid.WordCount);
        Assert.Equal(9, second.Bid.WordCount);
        Assert.NotEqual(first.Id, second.Id);
    }

    private static async Task<(GameEngine Engine, IGameSessionStore Store, Guid Id, Player[] Players)> Game()
    {
        var clock = new ManualSessionClock();
        var store = SessionTestData.Store(clock);
        var engine = SessionTestData.Engine(store, clock);
        var players = SessionTestData.Players();
        var id = (await engine.CreateAsync(players)).State!.Id;
        Assert.True((await engine.InitializeAsync(id)).Succeeded);
        Assert.True((await engine.StartAsync(id)).Succeeded);
        Assert.True((await engine.SelectCategoryAsync(id, players[0].Id, "cars", new(Guid.NewGuid(), (await store.GetAsync(id))!.Version))).Succeeded);
        Assert.True((await engine.PlaceBidAsync(id, players[0].Id, 3, new(Guid.NewGuid(), (await store.GetAsync(id))!.Version))).Succeeded);
        return (engine, store, id, players);
    }
}
