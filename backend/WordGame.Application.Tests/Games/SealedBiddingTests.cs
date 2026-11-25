using WordGame.Application.Games;
using WordGame.Domain;

namespace WordGame.Application.Tests.Games;

public sealed class SealedBiddingTests
{
    [Fact]
    public async Task PartyFinaleRevealsOnlyAfterAllBidsAndThenAllowsAChallenge()
    {
        var game = await TurnTestGame.Create(2, settings: MatchSettings.Party(1, sealedOpening: true));
        await game.Select();
        var first = TurnTestGame.Accepted(await game.Engine.SubmitSealedBidAsync(game.Id, game.Players[0].Id,
            2, await game.Context()));
        Assert.True(first.Game.CurrentRound!.SealedOpeningPending);
        Assert.Null(first.Game.CurrentRound.HighestBid);
        Assert.Equal(GameEngineErrorCode.InvalidState,
            (await game.Engine.PlaceBidAsync(game.Id, game.Players[0].Id, 3, await game.Context())).Error!.Code);
        var second = TurnTestGame.Accepted(await game.Engine.SubmitSealedBidAsync(game.Id, game.Players[1].Id,
            3, await game.Context(), risk: true));
        Assert.False(second.Game.CurrentRound!.SealedOpeningPending);
        Assert.Equal(game.Players[1].Id, second.Game.CurrentRound.HighestBid!.PlayerId);
        Assert.True(second.Game.CurrentRound.HighestBid.Risk);
        Assert.Equal(game.Players[0].Id, second.Game.CurrentRound.Turn.PlayerId);
        Assert.Equal(RoundStatus.Challenge, (await game.Bluff(0)).Game.CurrentRound!.Status);
    }
}
