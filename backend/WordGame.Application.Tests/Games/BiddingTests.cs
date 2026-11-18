using WordGame.Application.Games;
using WordGame.Domain;

namespace WordGame.Application.Tests.Games;

public sealed class BiddingTests
{
    [Theory]
    [InlineData(2, 1)]
    [InlineData(3, 3)]
    [InlineData(4, 25)]
    public async Task OpeningBidHasNoImplicitMinimumBeyondPositiveAndTracksItsOwner(int players, int openingBid)
    {
        var game = await TurnTestGame.Create(players);
        var before = await game.State();
        Assert.Null(before.Game.CurrentRound!.HighestBid);
        Assert.Null(before.Game.CurrentRound.Challenge);
        var selected = await game.Select();
        Assert.Equal(RoundStatus.Bidding, selected.Game.CurrentRound!.Status);
        Assert.Null(selected.Game.CurrentRound.HighestBid);
        Assert.Equal(game.Players[0].Id, selected.Game.CurrentRound.Turn.PlayerId);
        var bid = await game.Bid(0, openingBid);
        Assert.Equal(new Bid(game.Players[0].Id, openingBid), bid.Game.CurrentRound!.HighestBid);
        Assert.Equal(game.Players[1].Id, bid.Game.CurrentRound.Turn.PlayerId);
        Assert.Equal(selected.Version + 1, bid.Version);
        Assert.Null(bid.Game.CurrentRound.Challenge);
        // Claims may exceed dataset capacity; accepting a bid does not promise challenge success.
        Assert.Single(bid.SelectedCategory!.Definition.Answers);
    }

    [Fact]
    public async Task IssueExampleChallengesTheLatestBidderAndLatestAmount()
    {
        var game = await TurnTestGame.Create(2);
        await game.Select();
        await game.Bid(0, 3);
        var raised = await game.Bid(1, 5);
        var state = await game.Bluff(0);
        var round = state.Game.CurrentRound!;
        var challenge = round.Challenge!;
        Assert.Equal(RoundStatus.Challenge, round.Status);
        Assert.Equal(raised.Game.CurrentRound!.HighestBid, challenge.Bid);
        Assert.Equal(5, challenge.Bid.WordCount);
        Assert.Equal(game.Players[1].Id, challenge.Bid.PlayerId);
        Assert.Equal(game.Players[0].Id, challenge.ChallengerId);
        Assert.Equal(game.Players[1].Id, round.Turn.PlayerId);
        Assert.Equal(game.Clock.GetUtcNow(), challenge.StartedAt);
        Assert.Equal(challenge.StartedAt + TurnTestGame.Duration, challenge.Deadline);
        Assert.Equal(ChallengeOutcome.Pending, challenge.Outcome);
        Assert.Empty(challenge.Submissions);
    }

    [Theory]
    [InlineData(0, GameEngineErrorCode.InvalidAction)]
    [InlineData(-1, GameEngineErrorCode.InvalidAction)]
    [InlineData(int.MinValue, GameEngineErrorCode.InvalidAction)]
    [InlineData(4, GameEngineErrorCode.InvalidState)]
    [InlineData(5, GameEngineErrorCode.InvalidState)]
    public async Task NonpositiveEqualAndLowerBidsCannotChangeTheHighestBidOrTurn(int count, GameEngineErrorCode code)
    {
        var game = await TurnTestGame.Create(2);
        await game.Select();
        await game.Bid(0, 5);
        await Rejected(game, () => Bid(game, game.Players[1].Id, count), code);
        Assert.Equal(6, (await game.Bid(1, 6)).Game.CurrentRound!.HighestBid!.WordCount);
    }

    [Fact]
    public async Task OutsidersAndOutOfTurnPlayersCannotBidOrCallBluff()
    {
        var game = await TurnTestGame.Create(4);
        await game.Select();
        await game.Bid(0, 3);
        foreach (var actor in new[] { game.Players[0].Id, game.Players[2].Id, Guid.NewGuid() })
        {
            var code = game.Players.Any(player => player.Id == actor) ? GameEngineErrorCode.NotYourTurn : GameEngineErrorCode.PlayerNotInGame;
            await Rejected(game, () => Bid(game, actor, 5), code);
            await Rejected(game, () => Bluff(game, actor), code);
        }
        Assert.Equal(game.Players[0].Id, (await game.Bluff(1)).Game.CurrentRound!.Challenge!.Bid.PlayerId);
    }

    [Fact]
    public async Task DepartedActorsAndAnExpiredHighestBidderCannotProduceABluffChallenge()
    {
        var game = await TurnTestGame.Create(3);
        await game.Select();
        await game.Bid(0, 3);
        TurnTestGame.Accepted(await game.Engine.LeaveAsync(game.Id, game.Players[2].Id, await game.Context()));
        await Rejected(game, () => Bid(game, game.Players[2].Id, 5), GameEngineErrorCode.PlayerUnavailable);
        await Rejected(game, () => Bluff(game, game.Players[2].Id), GameEngineErrorCode.PlayerUnavailable);
        await game.Disconnect(0);
        game.Clock.Advance(TurnTestGame.Grace);
        await Rejected(game, () => Bluff(game, game.Players[1].Id), GameEngineErrorCode.InvalidState);
        Assert.Null((await game.State()).Game.CurrentRound!.Challenge);
    }

    [Fact]
    public async Task BidsNeedASelectedProviderSnapshotAndBluffNeedsAnExistingBid()
    {
        var game = await TurnTestGame.Create();
        await Rejected(game, () => Bid(game, game.Players[0].Id, 1), GameEngineErrorCode.InvalidState);
        await Rejected(game, () => Bluff(game, game.Players[0].Id), GameEngineErrorCode.InvalidState);
        // A trusted low-level metadata mutation is insufficient to bypass provider-backed engine selection.
        await game.Store.UpdateAsync(game.Id, session => session.Game.CurrentRound!.SelectCategory(game.Players[0].Id, TurnTestGame.Category));
        await Rejected(game, () => Bid(game, game.Players[0].Id, 1), GameEngineErrorCode.InvalidState);
        var valid = await TurnTestGame.Create();
        await valid.Select();
        await Rejected(valid, () => Bluff(valid, valid.Players[0].Id), GameEngineErrorCode.InvalidState);
    }

    [Fact]
    public async Task OwnBidCannotBeChallengedEvenAfterATrustedTurnReassignment()
    {
        var game = await TurnTestGame.Create();
        await game.Select();
        await game.Bid(0, 3);
        await game.Store.UpdateAsync(game.Id, session => session.Game.CurrentRound!.AssignTurn(game.Players[0].Id));
        await Rejected(game, () => Bluff(game, game.Players[0].Id), GameEngineErrorCode.InvalidAction);
    }

    [Fact]
    public async Task NeitherParticipantCanBidOrCreateAnotherChallengeAfterABluffCall()
    {
        var game = await TurnTestGame.Create(2);
        await game.Select();
        await game.Bid(0, 3);
        await game.Bluff(1);
        foreach (var actor in game.Players)
        {
            await Rejected(game, () => Bid(game, actor.Id, 4), GameEngineErrorCode.InvalidState);
            await Rejected(game, () => Bluff(game, actor.Id), GameEngineErrorCode.InvalidState);
        }
    }

    [Fact]
    public async Task BidAndBluffIdsCannotBeReusedWithFreshVersionsOrChangedPayloads()
    {
        var game = await TurnTestGame.Create(2);
        await game.Select();
        var bidContext = await game.Context();
        TurnTestGame.Accepted(await Bid(game, game.Players[0].Id, 3, bidContext));
        await game.Bid(1, 5);
        await Rejected(game, () => Bid(game, game.Players[0].Id, 6, bidContext), GameEngineErrorCode.StaleState);
        var freshDuplicate = await game.Context(bidContext.ActionId);
        await Rejected(game, () => Bid(game, game.Players[0].Id, 6, freshDuplicate), GameEngineErrorCode.DuplicateAction);
        await game.Bid(0, 6);
        var bluffContext = await game.Context();
        TurnTestGame.Accepted(await Bluff(game, game.Players[1].Id, bluffContext));
        var duplicateBluff = await game.Context(bluffContext.ActionId);
        await Rejected(game, () => Bluff(game, game.Players[1].Id, duplicateBluff), GameEngineErrorCode.DuplicateAction);
    }

    [Fact]
    public async Task MaximumIntegerBidCanBeChallengedWithoutOverflowOrAnImplicitRaise()
    {
        var game = await TurnTestGame.Create(2);
        await game.Select();
        await game.Bid(0, int.MaxValue);
        await Rejected(game, () => Bid(game, game.Players[1].Id, int.MaxValue), GameEngineErrorCode.InvalidState);
        Assert.Equal(int.MaxValue, (await game.Bluff(1)).Game.CurrentRound!.Challenge!.Bid.WordCount);
    }

    private static async Task<GameEngineResult> Bid(TurnTestGame game, Guid actor, int count, PlayerActionContext? context = null) =>
        await game.Engine.PlaceBidAsync(game.Id, actor, count, context ?? await game.Context());
    private static async Task<GameEngineResult> Bluff(TurnTestGame game, Guid actor, PlayerActionContext? context = null) =>
        await game.Engine.CallBluffAsync(game.Id, actor, TurnTestGame.Duration, context ?? await game.Context());

    private static async Task Rejected(TurnTestGame game, Func<Task<GameEngineResult>> action, GameEngineErrorCode code)
    {
        var before = await game.State();
        var result = await action();
        Assert.False(result.Succeeded);
        Assert.Null(result.State);
        Assert.Equal(code, result.Error!.Code);
        var after = await game.State();
        Assert.Equal(before.Version, after.Version);
        Assert.Equal(before.UpdatedAt, after.UpdatedAt);
        Assert.Equal(before.Game.CurrentRound!.Status, after.Game.CurrentRound!.Status);
        Assert.Equal(before.Game.CurrentRound.Turn, after.Game.CurrentRound.Turn);
        Assert.Equal(before.Game.CurrentRound.HighestBid, after.Game.CurrentRound.HighestBid);
        Assert.Equal(before.Game.CurrentRound.Challenge?.Id, after.Game.CurrentRound.Challenge?.Id);
        Assert.Equal(before.Game.CurrentRound.Challenge?.Deadline, after.Game.CurrentRound.Challenge?.Deadline);
    }
}
