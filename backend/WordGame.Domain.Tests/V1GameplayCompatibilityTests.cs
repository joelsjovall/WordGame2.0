namespace WordGame.Domain.Tests;

public sealed class V1GameplayCompatibilityTests
{
    [Theory]
    [InlineData(2, true)]
    [InlineData(2, false)]
    [InlineData(4, true)]
    [InlineData(4, false)]
    public void ReviewedBidChallengeAndRoundProgressionRulesWorkWithV2Models(int playerCount, bool succeeded)
    {
        var game = DomainTestData.CreateGame(playerCount);
        var firstPlayer = game.Players[0].Id;
        var secondPlayer = game.GetNextPlayerId(firstPlayer);
        var round = game.StartRound(Guid.NewGuid(), firstPlayer);
        round.SelectCategory(firstPlayer, new Category("cars", "Car manufacturers", CategoryDifficulty.Easy));
        round.RecordBid(new Bid(firstPlayer, 1));
        round.AssignTurn(secondPlayer);
        round.RecordBid(new Bid(secondPlayer, 2));
        var caller = game.GetNextPlayerId(secondPlayer);
        round.AssignTurn(caller);
        var challenge = new Challenge(Guid.NewGuid(), round.HighestBid!, caller,
            DomainTestData.StartedAt, DomainTestData.StartedAt.AddSeconds(30));
        round.BeginChallenge(challenge);

        Assert.Equal(secondPlayer, round.Turn.PlayerId);
        Assert.Equal(2, challenge.Bid.WordCount);
        Assert.Throws<InvalidOperationException>(() => round.RecordBid(new Bid(secondPlayer, 3)));
        challenge.RecordSubmission(new SubmittedWord(secondPlayer, " Volvo ", DomainTestData.StartedAt.AddSeconds(1),
            WordSubmissionStatus.Accepted, "volvo"));
        challenge.RecordSubmission(new SubmittedWord(secondPlayer, "VOLVO", DomainTestData.StartedAt.AddSeconds(2),
            WordSubmissionStatus.Duplicate, "volvo"));
        challenge.RecordSubmission(new SubmittedWord(secondPlayer, "not-a-car", DomainTestData.StartedAt.AddSeconds(3),
            WordSubmissionStatus.Rejected));
        Assert.Equal(1, challenge.AcceptedWordCount);
        if (succeeded)
        {
            challenge.RecordSubmission(new SubmittedWord(secondPlayer, "BMW", DomainTestData.StartedAt.AddSeconds(4),
                WordSubmissionStatus.Accepted, "bmw"));
        }

        var outcome = succeeded ? ChallengeOutcome.Succeeded : ChallengeOutcome.Failed;
        challenge.Resolve(outcome);
        round.Complete();
        Assert.Equal(outcome, challenge.Outcome);
        Assert.Equal(RoundStatus.Completed, round.Status);
        Assert.Throws<InvalidOperationException>(() => challenge.Resolve(outcome));

        // V1 advanced to the player after the challenged bidder. The engine will choose this policy later.
        var nextStarter = game.GetNextPlayerId(challenge.Bid.PlayerId);
        var nextRound = game.StartRound(Guid.NewGuid(), nextStarter);
        Assert.Equal(2, nextRound.Number);
        Assert.Equal(nextStarter, nextRound.Turn.PlayerId);
        Assert.Equal(RoundStatus.CategorySelection, nextRound.Status);
        Assert.Null(nextRound.HighestBid);
        Assert.Null(nextRound.Challenge);
        // F005 intentionally selects no scoring formula.
        Assert.All(game.Scores.Values, score => Assert.Equal(0, score.Points));
    }
}
