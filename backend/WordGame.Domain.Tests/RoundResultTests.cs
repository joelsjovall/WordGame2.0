namespace WordGame.Domain.Tests;

public sealed class RoundResultTests
{
    [Fact]
    public void ResultsFreezeAllPlayerDeltasAndTotalsAndSurviveCopiesAndLaterRounds()
    {
        var game = DomainTestData.CreateGame(4);
        var round = DomainTestData.CompleteRound(game);
        game.RecordRoundScores(round.Id, [new PlayerScore(game.Players[1].Id, 50)]);
        var result = game.RoundResults[round.Id];
        Assert.Equal(round.Id, result.RoundId);
        Assert.Equal(round.Challenge!.Id, result.ChallengeId);
        Assert.Equal(round.Category, result.Category);
        Assert.Equal(game.Players[0].Id, result.BidderId);
        Assert.Equal(game.Players[1].Id, result.CallerId);
        Assert.Equal(game.Players[1].Id, result.WinnerId);
        Assert.Equal(4, result.Scores.Count);
        Assert.Equal(50, result.Scores[1].PointsAwarded);
        Assert.Equal(50, result.Scores[1].TotalPoints);
        Assert.All(result.Scores.Where(score => score.PlayerId != result.WinnerId), score =>
        {
            Assert.Equal(0, score.PointsAwarded);
            Assert.Equal(0, score.TotalPoints);
        });
        Assert.Throws<NotSupportedException>(() => ((IList<RoundPlayerScore>)result.Scores).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<SubmittedWord>)result.Submissions).Clear());
        Assert.Throws<NotSupportedException>(() => ((IDictionary<Guid, RoundResult>)game.RoundResults).Clear());
        var copy = game.Copy();
        Assert.Same(result, copy.RoundResults[round.Id]);
        var second = DomainTestData.CompleteRound(copy);
        copy.RecordRoundScores(second.Id, [new PlayerScore(result.WinnerId, 10)]);
        Assert.Equal(50, result.Scores[1].TotalPoints);
        Assert.Equal(60, copy.RoundResults[second.Id].Scores[1].TotalPoints);
        Assert.Single(game.RoundResults);
        Assert.Equal(2, copy.RoundResults.Count);
        Assert.Throws<InvalidOperationException>(() => copy.RecordRoundScores(round.Id, []));
    }

    [Fact]
    public void FailedAwardBatchPublishesNoResultAndCanBeRetriedWithValidAwards()
    {
        var game = DomainTestData.CreateGame();
        var round = DomainTestData.CompleteRound(game);
        game.RecordRoundScores(round.Id, [new PlayerScore(game.Players[0].Id, int.MaxValue)]);
        var next = DomainTestData.CompleteRound(game);
        Assert.Throws<OverflowException>(() => game.RecordRoundScores(next.Id, [new PlayerScore(game.Players[0].Id, 1)]));
        Assert.False(game.RoundResults.ContainsKey(next.Id));
        game.RecordRoundScores(next.Id, []);
        Assert.Equal(0, game.RoundResults[next.Id].Scores[0].PointsAwarded);
        Assert.Equal(int.MaxValue, game.RoundResults[next.Id].Scores[0].TotalPoints);
    }
}
