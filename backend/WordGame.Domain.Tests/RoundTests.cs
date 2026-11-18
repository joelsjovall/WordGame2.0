namespace WordGame.Domain.Tests;

public sealed class RoundTests
{
    [Fact]
    public void CategorySelectionRequiresTheCurrentPlayerAndLocksOnceBiddingStarts()
    {
        var game = DomainTestData.CreateGame();
        var round = game.StartRound(Guid.NewGuid(), game.Players[0].Id);
        var category = new Category("capitals", "Capitals", CategoryDifficulty.Easy);

        Assert.Throws<InvalidOperationException>(() => round.SelectCategory(game.Players[1].Id, category));
        round.SelectCategory(game.Players[0].Id, category);
        Assert.Equal(RoundStatus.Bidding, round.Status);
        Assert.Same(category, round.Category);
        Assert.Throws<InvalidOperationException>(() => round.SelectCategory(game.Players[0].Id, category));
    }

    [Fact]
    public void BidsRequireTheBiddingPhaseCurrentPlayerAndAnIncrease()
    {
        var game = DomainTestData.CreateGame();
        var round = game.StartRound(Guid.NewGuid(), game.Players[0].Id);
        var aliceBid = new Bid(game.Players[0].Id, 2);
        Assert.Throws<InvalidOperationException>(() => round.RecordBid(aliceBid));
        round.SelectCategory(game.Players[0].Id, new Category("id", "Name", CategoryDifficulty.Easy));
        Assert.Throws<InvalidOperationException>(() => round.RecordBid(new Bid(game.Players[1].Id, 2)));
        round.RecordBid(aliceBid);
        Assert.Throws<InvalidOperationException>(() => round.RecordBid(aliceBid));
        Assert.Throws<InvalidOperationException>(() => round.RecordBid(new Bid(aliceBid.PlayerId, 1)));
        round.AssignTurn(game.Players[1].Id);
        var raised = new Bid(game.Players[1].Id, 3);
        round.RecordBid(raised);
        Assert.Same(raised, round.HighestBid);
    }

    [Fact]
    public void TurnsStayWithinTheRosterAndAdvanceTheirSequence()
    {
        var game = DomainTestData.CreateGame(4);
        var originalOrder = game.Players.Select(player => player.Id).ToArray();
        var round = game.StartRound(Guid.NewGuid(), game.Players[0].Id);
        Assert.Throws<InvalidOperationException>(() => round.AssignTurn(Guid.NewGuid()));
        Assert.Throws<InvalidOperationException>(() => round.AssignTurn(game.Players[0].Id));
        round.AssignTurn(game.Players[3].Id);
        Assert.Equal(new PlayerTurn(game.Players[3].Id, 2), round.Turn);
        Assert.Equal(originalOrder, game.Players.Select(player => player.Id));
    }

    [Fact]
    public void ChallengeRequiresTheCurrentBidAndCurrentCaller()
    {
        var game = DomainTestData.CreateGame(3);
        var round = DomainTestData.StartBidding(game);
        var start = DomainTestData.StartedAt;
        var wrongBid = new Challenge(Guid.NewGuid(), new Bid(game.Players[0].Id, 3),
            game.Players[1].Id, start, start.AddSeconds(30));
        var wrongCaller = new Challenge(Guid.NewGuid(), round.HighestBid!,
            game.Players[2].Id, start, start.AddSeconds(30));

        Assert.Throws<InvalidOperationException>(() => round.BeginChallenge(wrongBid));
        Assert.Throws<InvalidOperationException>(() => round.BeginChallenge(wrongCaller));
        var challenge = DomainTestData.BeginChallenge(game, round);
        Assert.Same(challenge, round.Challenge);
        Assert.Equal(game.Players[0].Id, round.Turn.PlayerId);
        Assert.Equal(RoundStatus.Challenge, round.Status);
        Assert.Throws<InvalidOperationException>(() => round.RecordBid(new Bid(game.Players[0].Id, 4)));
        Assert.Throws<InvalidOperationException>(() => round.AssignTurn(game.Players[1].Id));
    }

    [Fact]
    public void RoundsRequireResolvedChallengesAndRejectRepeatedCompletion()
    {
        var game = DomainTestData.CreateGame();
        var round = DomainTestData.StartBidding(game);
        Assert.Throws<InvalidOperationException>(() => round.Complete());
        var challenge = DomainTestData.BeginChallenge(game, round);
        Assert.Throws<InvalidOperationException>(() => round.Complete());
        challenge.Resolve(ChallengeOutcome.Failed);
        round.Complete();

        Assert.Equal(RoundStatus.Completed, round.Status);
        Assert.Throws<InvalidOperationException>(() => round.Complete());
        Assert.Throws<InvalidOperationException>(() => round.AssignTurn(game.Players[1].Id));
    }

    [Fact]
    public void MutableChallengesCannotBeAttachedToMultipleGames()
    {
        var game = DomainTestData.CreateGame();
        var other = new Game(Guid.NewGuid(), game.Players);
        var round = DomainTestData.StartBidding(game);
        var otherRound = DomainTestData.StartBidding(other);
        var challenge = DomainTestData.BeginChallenge(game, round);

        Assert.Throws<InvalidOperationException>(() => otherRound.BeginChallenge(challenge));
        Assert.Equal(RoundStatus.Bidding, otherRound.Status);
        Assert.Null(otherRound.Challenge);
    }
}
