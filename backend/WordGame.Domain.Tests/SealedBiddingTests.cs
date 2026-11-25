namespace WordGame.Domain.Tests;

public sealed class SealedBiddingTests
{
    [Fact]
    public void SealedBidsStayHiddenUntilEveryPlayerSubmitsAndRevealDeterministically()
    {
        var players = DomainTestData.CreateGame().Players;
        var game = new Game(Guid.NewGuid(), players, MatchSettings.Party(1, sealedOpening: true));
        var round = game.StartRound(Guid.NewGuid(), players[0].Id);
        round.SelectCategory(players[0].Id, new Category("capitals", "Capitals", CategoryDifficulty.Easy));
        Assert.True(round.SealedOpeningPending);
        round.SubmitSealedBid(new Bid(players[0].Id, 3));
        Assert.Null(round.HighestBid);
        Assert.Null(game.Copy().CurrentRound!.HighestBid);
        Assert.Single(game.Copy().CurrentRound!.SealedBidSubmitterIds);
        Assert.Throws<InvalidOperationException>(() => round.RecordBid(new Bid(players[0].Id, 4)));
        Assert.Throws<InvalidOperationException>(() => round.SubmitSealedBid(new Bid(players[0].Id, 4)));

        round.SubmitSealedBid(new Bid(players[1].Id, 3, risk: true));
        Assert.Equal(players[0].Id, round.RevealSealedBids(players.Select(player => player.Id).ToArray()).PlayerId);
        Assert.False(round.SealedOpeningPending);
        Assert.Equal(players[1].Id, round.Turn.PlayerId);
        round.RecordBid(new Bid(players[1].Id, 4));
        Assert.Equal(4, round.HighestBid!.WordCount);
    }
}
