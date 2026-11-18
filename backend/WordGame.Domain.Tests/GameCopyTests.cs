namespace WordGame.Domain.Tests;

public sealed class GameCopyTests
{
    [Fact]
    public void CopySeparatesMutableRoundsChallengesAndSubmissionLists()
    {
        var game = DomainTestData.CreateGame();
        var round = DomainTestData.StartBidding(game);
        var challenge = DomainTestData.BeginChallenge(game, round);
        challenge.RecordSubmission(DomainTestData.AcceptedWord(challenge));

        var copy = game.Copy();
        Assert.Equal(game.Id, copy.Id);
        Assert.NotSame(game.CurrentRound, copy.CurrentRound);
        Assert.NotSame(challenge, copy.CurrentRound!.Challenge);
        copy.CurrentRound.Challenge!.RecordSubmission(DomainTestData.AcceptedWord(challenge, "paris"));
        Assert.Equal(1, challenge.AcceptedWordCount);
        Assert.Equal(2, copy.CurrentRound.Challenge.AcceptedWordCount);
        copy.Cancel();
        Assert.Equal(GameStatus.InProgress, game.Status);
        Assert.Equal(RoundStatus.Challenge, round.Status);
        Assert.Equal(ChallengeOutcome.Pending, challenge.Outcome);
    }

    [Fact]
    public void CopyPreservesScoreReplayGuardsWithoutSharingScoreTotals()
    {
        var game = DomainTestData.CreateGame();
        var first = DomainTestData.CompleteRound(game);
        var player = game.Players[0].Id;
        game.RecordRoundScores(first.Id, [new PlayerScore(player, 2)]);
        var copy = game.Copy();
        Assert.Throws<InvalidOperationException>(() =>
            copy.RecordRoundScores(first.Id, [new PlayerScore(player, 2)]));
        var second = DomainTestData.CompleteRound(copy);
        copy.RecordRoundScores(second.Id, [new PlayerScore(player, 3)]);

        Assert.Equal(2, game.Scores[player].Points);
        Assert.Equal(5, copy.Scores[player].Points);
        Assert.Single(game.Rounds);
        Assert.Equal(2, copy.Rounds.Count);
    }
}
