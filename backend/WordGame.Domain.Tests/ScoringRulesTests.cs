namespace WordGame.Domain.Tests;

public sealed class ScoringRulesTests
{
    [Theory]
    [InlineData(CategoryDifficulty.Easy, 1)]
    [InlineData(CategoryDifficulty.Medium, 2)]
    [InlineData(CategoryDifficulty.Hard, 3)]
    public void DefaultWordPointsAreExplicitForEachDifficulty(CategoryDifficulty difficulty, int expected) =>
        Assert.Equal(expected, ScoringRules.DefaultPointsPerWord(difficulty));

    [Fact]
    public void UnknownDifficultyHasNoImplicitScore() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ScoringRules.DefaultPointsPerWord((CategoryDifficulty)99));

    [Theory]
    [InlineData(1, true, 52, 0)]
    [InlineData(2, true, 54, 0)]
    [InlineData(3, true, 56, 0)]
    [InlineData(1, false, 1, 50)]
    [InlineData(2, false, 2, 50)]
    [InlineData(3, false, 3, 50)]
    public void AwardsWordPointsAndOutcomeBonusToTheCorrectPlayers(int multiplier, bool success, int bidder, int caller)
    {
        var (game, round) = Complete(success);
        game.RecordRoundScores(round.Id, new ScoringRules().Calculate(round, multiplier));
        Assert.Equal(bidder, game.Scores[game.Players[0].Id].Points);
        Assert.Equal(caller, game.Scores[game.Players[1].Id].Points);
        Assert.Throws<InvalidOperationException>(() => game.RecordRoundScores(round.Id, new ScoringRules().Calculate(round, multiplier)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public void BonusCanBeConfiguredOrDisabled(int bonus)
    {
        var (_, round) = Complete(true);
        Assert.Equal(4 + bonus, Assert.Single(new ScoringRules(bonus).Calculate(round, 2)).Points);
    }

    [Theory]
    [InlineData(true, 102, 0)]
    [InlineData(false, 1, 100)]
    public void RiskBidDoublesTheOutcomeBonusForTheChallengeWinner(bool success, int bidder, int caller)
    {
        var game = DomainTestData.CreateGame();
        var round = game.StartRound(Guid.NewGuid(), game.Players[0].Id);
        round.SelectCategory(game.Players[0].Id, new Category("capitals", "Capitals", CategoryDifficulty.Easy));
        round.RecordBid(new Bid(game.Players[0].Id, 2, risk: true));
        round.AssignTurn(game.Players[1].Id);
        var challenge = new Challenge(Guid.NewGuid(), round.HighestBid!, game.Players[1].Id,
            DomainTestData.StartedAt, DomainTestData.StartedAt.AddSeconds(30));
        round.BeginChallenge(challenge);
        challenge.RecordSubmission(DomainTestData.AcceptedWord(challenge));
        if (success)
        {
            challenge.RecordSubmission(DomainTestData.AcceptedWord(challenge, "paris"));
        }
        challenge.Resolve(success ? ChallengeOutcome.Succeeded : ChallengeOutcome.Failed);
        round.Complete();

        game.RecordRoundScores(round.Id, new ScoringRules().Calculate(round, 1));
        Assert.Equal(bidder, game.Scores[game.Players[0].Id].Points);
        Assert.Equal(caller, game.Scores[game.Players[1].Id].Points);
        Assert.True(game.RoundResults[round.Id].Risk);
    }

    [Fact]
    public void InvalidSettingsAndIncompleteRoundsAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ScoringRules(-1));
        var game = DomainTestData.CreateGame();
        var round = DomainTestData.StartBidding(game);
        var rules = new ScoringRules();
        Assert.Throws<ArgumentNullException>(() => rules.Calculate(null!, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => rules.Calculate(round, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => rules.Calculate(round, -1));
        Assert.Throws<InvalidOperationException>(() => rules.Calculate(round, 1));
        DomainTestData.BeginChallenge(game, round);
        Assert.Throws<InvalidOperationException>(() => rules.Calculate(round, 1));
        game.Cancel();
        Assert.Throws<InvalidOperationException>(() => rules.Calculate(round, 1));
    }

    [Fact]
    public void AwardOverflowIsDetected()
    {
        var (_, round) = Complete(true);
        Assert.Throws<OverflowException>(() => new ScoringRules().Calculate(round, int.MaxValue));
        Assert.Throws<OverflowException>(() => new ScoringRules(int.MaxValue).Calculate(round, 1));
    }

    private static (Game Game, GameRound Round) Complete(bool success)
    {
        var game = DomainTestData.CreateGame();
        var round = DomainTestData.StartBidding(game);
        var challenge = DomainTestData.BeginChallenge(game, round);
        challenge.RecordSubmission(DomainTestData.AcceptedWord(challenge));
        if (success)
        {
            challenge.RecordSubmission(DomainTestData.AcceptedWord(challenge, "paris"));
        }
        challenge.Resolve(success ? ChallengeOutcome.Succeeded : ChallengeOutcome.Failed);
        round.Complete();
        return (game, round);
    }
}
