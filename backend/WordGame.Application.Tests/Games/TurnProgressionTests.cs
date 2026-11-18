using WordGame.Application.Games;
using WordGame.Application.Sessions;
using WordGame.Domain;

namespace WordGame.Application.Tests.Games;

public sealed class TurnProgressionTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task StableRosterOrderWrapsAndNextRoundRotatesFromTheOriginalStarter(int count)
    {
        var game = await TurnTestGame.Create(count);
        Assert.Equal(game.Players[0].Id, (await game.State()).Game.CurrentRound!.StartingPlayerId);
        var selected = await game.Select();
        Assert.Equal(game.Players[0].Id, selected.Game.CurrentRound!.Turn.PlayerId);
        for (var bid = 1; bid <= count * 2; bid++)
        {
            var state = await game.Bid((bid - 1) % count, bid);
            Assert.Equal(game.Players[bid % count].Id, state.Game.CurrentRound!.Turn.PlayerId);
            Assert.Equal(bid + 1, state.Game.CurrentRound.Turn.Number);
            Assert.Equal(game.Players[0].Id, state.Game.Copy().CurrentRound!.StartingPlayerId);
        }
        var challenge = await game.Bluff(0);
        Assert.Equal(game.Players[count - 1].Id, challenge.Game.CurrentRound!.Turn.PlayerId);
        game.Clock.Advance(TurnTestGame.Duration);
        TurnTestGame.Accepted(await game.Engine.ResolveChallengeAsync(game.Id));
        var next = TurnTestGame.Accepted(await game.Engine.NextRoundAsync(game.Id));
        Assert.Equal(2, next.Game.CurrentRound!.Number);
        Assert.Equal(game.Players[1].Id, next.Game.CurrentRound.StartingPlayerId);
        Assert.Equal(game.Players[1].Id, next.Game.CurrentRound.Turn.PlayerId);
        Assert.Equal(1, next.Game.CurrentRound.Turn.Number);
    }

    [Fact]
    public async Task InitialStarterIsTheFirstEligibleRosterMember()
    {
        var clock = new EngineTestClock();
        var store = new EngineTestStore(clock);
        var engine = TestCategoryProvider.Engine(store, new SessionRetention(TurnTestGame.Grace, TurnTestGame.Grace, TurnTestGame.Grace));
        var players = Enumerable.Range(0, 4).Select(index => new Player(Guid.NewGuid(), $"Player {index}")).ToArray();
        var created = TurnTestGame.Accepted(await engine.CreateAsync(players));
        TurnTestGame.Accepted(await engine.LeaveAsync(created.Id, players[0].Id, new(Guid.NewGuid(), created.Version)));
        TurnTestGame.Accepted(await engine.InitializeAsync(created.Id));
        var started = TurnTestGame.Accepted(await engine.StartAsync(created.Id));
        Assert.Equal(players[1].Id, started.Game.CurrentRound!.StartingPlayerId);
    }

    [Fact]
    public async Task NextRoundSkipsADepartedStarterAndRejectsActionsFromPreviousResults()
    {
        var game = await TurnTestGame.Create();
        await game.Select();
        await game.Bid(0, 1);
        await game.Bluff(1);
        game.Clock.Advance(TurnTestGame.Duration);
        TurnTestGame.Accepted(await game.Engine.ResolveChallengeAsync(game.Id));
        var before = await game.State();
        Assert.Equal(GameEngineErrorCode.InvalidState,
            (await game.Engine.PlaceBidAsync(game.Id, game.Players[0].Id, 2, await game.Context())).Error!.Code);
        Assert.Equal(before.Version, (await game.State()).Version);
        TurnTestGame.Accepted(await game.Engine.LeaveAsync(game.Id, game.Players[1].Id, await game.Context()));
        var next = TurnTestGame.Accepted(await game.Engine.NextRoundAsync(game.Id));
        Assert.Equal(game.Players[2].Id, next.Game.CurrentRound!.StartingPlayerId);
        Assert.Equal(game.Players[2].Id, next.Game.CurrentRound.Turn.PlayerId);
    }

    [Fact]
    public async Task LeavingSkipsThePlayerWithoutChangingRosterOrAllowingReentry()
    {
        var game = await TurnTestGame.Create(4);
        var left = TurnTestGame.Accepted(await game.Engine.LeaveAsync(game.Id, game.Players[0].Id, await game.Context()));
        Assert.Equal(4, left.Game.Players.Count);
        Assert.True(left.Players[0].HasLeft);
        Assert.Equal(0, left.Players[0].ConnectionCount);
        Assert.Equal(game.Players[1].Id, left.Game.CurrentRound!.Turn.PlayerId);
        Assert.Equal(2, left.Game.CurrentRound.Turn.Number);
        Assert.Equal(GameEngineErrorCode.PlayerUnavailable,
            (await game.Engine.SelectCategoryAsync(game.Id, game.Players[0].Id, TurnTestGame.Category.Id, await game.Context())).Error!.Code);
        Assert.Equal(GameEngineErrorCode.InvalidState,
            (await game.Engine.ConnectAsync(game.Id, game.Players[0].Id, Guid.NewGuid())).Error!.Code);
        await game.Select(1);
        await game.Bid(1, 1);
        await game.Bid(2, 2);
        var wrapped = await game.Bid(3, 3);
        Assert.Equal(game.Players[1].Id, wrapped.Game.CurrentRound!.Turn.PlayerId);
    }

    [Fact]
    public async Task LeavingInOneVersusOneCancelsWithoutInventingForfeitScores()
    {
        var game = await TurnTestGame.Create(2);
        var result = TurnTestGame.Accepted(await game.Engine.LeaveAsync(game.Id, game.Players[1].Id, await game.Context()));
        Assert.Equal(SessionStatus.Abandoned, result.Status);
        Assert.Equal(GameStatus.Cancelled, result.Game.Status);
        Assert.All(result.Game.Scores.Values, score => Assert.Equal(0, score.Points));
    }

    [Fact]
    public async Task DisconnectGraceSkipsAnExpiredActivePlayerAndReconnectPreservesOrder()
    {
        var game = await TurnTestGame.Create();
        await game.Disconnect(0);
        game.Clock.Advance(TurnTestGame.Grace - TimeSpan.FromTicks(1));
        await game.Reconcile();
        Assert.Equal(game.Players[0].Id, (await game.State()).Game.CurrentRound!.Turn.PlayerId);
        game.Clock.Advance(TimeSpan.FromTicks(1));
        var before = await game.State();
        Assert.Equal(GameEngineErrorCode.PlayerUnavailable,
            (await game.Engine.SelectCategoryAsync(game.Id, game.Players[0].Id, TurnTestGame.Category.Id, await game.Context())).Error!.Code);
        Assert.Equal(before.Version, (await game.State()).Version);
        var skipped = await game.Reconcile();
        Assert.Equal(game.Players[1].Id, skipped.Game.CurrentRound!.Turn.PlayerId);
        TurnTestGame.Accepted(await game.Engine.ConnectAsync(game.Id, game.Players[0].Id, Guid.NewGuid()));
        await game.Select(1);
        await game.Bid(1, 1);
        Assert.Equal(game.Players[0].Id, (await game.Bid(2, 2)).Game.CurrentRound!.Turn.PlayerId);
    }

    [Fact]
    public async Task ExpiredBiddingCallerIsSkippedWhileTheHighestBidStaysIntact()
    {
        var game = await TurnTestGame.Create();
        await game.Select();
        await game.Bid(0, 2);
        await game.Disconnect(1);
        game.Clock.Advance(TurnTestGame.Grace);
        var state = await game.Reconcile();
        Assert.Equal(game.Players[2].Id, state.Game.CurrentRound!.Turn.PlayerId);
        Assert.Equal(2, state.Game.CurrentRound.HighestBid!.WordCount);
        Assert.Equal(game.Players[0].Id, (await game.Bluff(2)).Game.CurrentRound!.Turn.PlayerId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task ExpiredEssentialChallengeParticipantCannotStallTheMatch(int participant)
    {
        var game = await TurnTestGame.Create();
        await game.Select();
        await game.Bid(0, 1);
        await game.Bluff(1);
        await game.Disconnect(participant);
        game.Clock.Advance(TurnTestGame.Grace);
        var result = await game.Reconcile();
        Assert.Equal(SessionStatus.Abandoned, result.Status);
        Assert.Equal(ChallengeOutcome.Cancelled, result.Game.CurrentRound!.Challenge!.Outcome);
    }

    [Fact]
    public async Task AnotherTabAndReconnectBeforeGraceDoNotMoveTheTurn()
    {
        var game = await TurnTestGame.Create();
        var secondTab = Guid.NewGuid();
        TurnTestGame.Accepted(await game.Engine.ConnectAsync(game.Id, game.Players[0].Id, secondTab));
        await game.Disconnect(0);
        game.Clock.Advance(TurnTestGame.Grace);
        await game.Disconnect(0); // Delayed duplicate disconnect from the old tab.
        Assert.Equal(game.Players[0].Id, (await game.Reconcile()).Game.CurrentRound!.Turn.PlayerId);
        TurnTestGame.Accepted(await game.Engine.DisconnectAsync(game.Id, game.Players[0].Id, secondTab));
        game.Clock.Advance(TurnTestGame.Grace - TimeSpan.FromTicks(1));
        TurnTestGame.Accepted(await game.Engine.ConnectAsync(game.Id, game.Players[0].Id, Guid.NewGuid()));
        game.Clock.Advance(TurnTestGame.Grace);
        Assert.Equal(game.Players[0].Id, (await game.Reconcile()).Game.CurrentRound!.Turn.PlayerId);
    }
}
