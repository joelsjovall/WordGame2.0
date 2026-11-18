namespace WordGame.Domain.Tests;

public sealed class GameTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void BothModesUseTheSameGameWithZeroStartingScores(int playerCount)
    {
        var game = DomainTestData.CreateGame(playerCount);

        Assert.Equal(GameStatus.WaitingToStart, game.Status);
        Assert.Equal(playerCount, game.Players.Count);
        Assert.All(game.Scores.Values, score => Assert.Equal(0, score.Points));
        Assert.Null(game.CurrentRound);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    public void RosterRejectsUnsupportedPlayerCounts(int playerCount)
    {
        Assert.Throws<ArgumentException>(() => DomainTestData.CreateGame(playerCount));
    }

    [Fact]
    public void RosterRejectsDuplicatesAndNullsAndEmptyGameId()
    {
        var alice = new Player(Guid.NewGuid(), "Alice");
        Assert.Throws<ArgumentException>(() => new Game(Guid.NewGuid(), [alice, alice]));
        Assert.Throws<ArgumentException>(() => new Game(Guid.NewGuid(), [alice, null!]));
        Assert.Throws<ArgumentNullException>(() => new Game(Guid.NewGuid(), null!));
        Assert.Throws<ArgumentException>(() =>
            new Game(Guid.Empty, [alice, new Player(Guid.NewGuid(), "Bob")]));
    }

    [Fact]
    public void CollectionsCannotBeMutatedFromOutsideTheGame()
    {
        var original = new[] { new Player(Guid.NewGuid(), "Alice"), new Player(Guid.NewGuid(), "Bob") };
        var game = new Game(Guid.NewGuid(), original);
        var alice = original[0];
        original[0] = new Player(Guid.NewGuid(), "Replacement");

        Assert.Same(alice, game.Players[0]);
        Assert.Throws<NotSupportedException>(() => ((IList<Player>)game.Players).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<GameRound>)game.Rounds).Clear());
        Assert.Throws<NotSupportedException>(() => ((IDictionary<Guid, PlayerScore>)game.Scores).Clear());
    }

    [Fact]
    public void InvalidRoundStartDoesNotChangeGameState()
    {
        var game = DomainTestData.CreateGame();
        Assert.Throws<InvalidOperationException>(() => game.StartRound(Guid.NewGuid(), Guid.NewGuid()));
        Assert.Throws<ArgumentException>(() => game.StartRound(Guid.Empty, game.Players[0].Id));

        Assert.Equal(GameStatus.WaitingToStart, game.Status);
        Assert.Empty(game.Rounds);
    }

    [Fact]
    public void RoundsAreSequentialAndCannotOverlapOrReuseIds()
    {
        var game = DomainTestData.CreateGame();
        var first = DomainTestData.CompleteRound(game);

        Assert.Equal(1, first.Number);
        Assert.Throws<InvalidOperationException>(() => game.StartRound(first.Id, game.Players[0].Id));
        var second = game.StartRound(Guid.NewGuid(), game.Players[1].Id);
        Assert.Equal(2, second.Number);
        Assert.Same(second, game.CurrentRound);
        Assert.Equal(game.Players[1].Id, second.Turn.PlayerId);
        Assert.Throws<InvalidOperationException>(() => game.StartRound(Guid.NewGuid(), game.Players[0].Id));
    }

    [Fact]
    public void CompletedGamesCannotRestartOrCancel()
    {
        var game = DomainTestData.CreateGame();
        Assert.Throws<InvalidOperationException>(() => game.Complete());
        DomainTestData.CompleteRound(game);
        game.Complete();

        Assert.Equal(GameStatus.Completed, game.Status);
        Assert.Throws<InvalidOperationException>(() => game.StartRound(Guid.NewGuid(), game.Players[0].Id));
        Assert.Throws<InvalidOperationException>(() => game.Cancel());
        Assert.Throws<InvalidOperationException>(() => game.Complete());
    }

    [Fact]
    public void CancellationEndsTheActiveRoundAndChallenge()
    {
        var game = DomainTestData.CreateGame();
        var round = DomainTestData.StartBidding(game);
        var challenge = DomainTestData.BeginChallenge(game, round);

        game.Cancel();

        Assert.Equal(GameStatus.Cancelled, game.Status);
        Assert.Equal(RoundStatus.Cancelled, round.Status);
        Assert.Equal(ChallengeOutcome.Cancelled, challenge.Outcome);
        Assert.Throws<InvalidOperationException>(() => challenge.RecordSubmission(DomainTestData.AcceptedWord(challenge)));
        Assert.Throws<InvalidOperationException>(() => challenge.Resolve(ChallengeOutcome.Failed));
        Assert.Throws<InvalidOperationException>(() => game.StartRound(Guid.NewGuid(), game.Players[0].Id));
    }

    [Fact]
    public void WaitingGameCanBeCancelledWithoutCreatingARound()
    {
        var game = DomainTestData.CreateGame();
        game.Cancel();
        Assert.Equal(GameStatus.Cancelled, game.Status);
        Assert.Empty(game.Rounds);
        Assert.Throws<InvalidOperationException>(() => game.Cancel());
    }

    [Fact]
    public void RoundAwardsAreRecordedOnceAndRemainIndependentAcrossGames()
    {
        var game = DomainTestData.CreateGame();
        var other = DomainTestData.CreateGame();
        var round = DomainTestData.CompleteRound(game);
        var winner = game.Players[1].Id;
        game.RecordRoundScores(round.Id, [new PlayerScore(winner, 3)]);

        Assert.Equal(3, game.Scores[winner].Points);
        Assert.Equal(0, game.Scores[game.Players[0].Id].Points);
        Assert.All(other.Scores.Values, score => Assert.Equal(0, score.Points));
        Assert.Throws<InvalidOperationException>(() =>
            game.RecordRoundScores(round.Id, [new PlayerScore(winner, 3)]));
    }

    [Fact]
    public void ScoringRejectsUnfinishedRoundsUnknownPlayersAndDuplicateAwards()
    {
        var game = DomainTestData.CreateGame();
        var active = game.StartRound(Guid.NewGuid(), game.Players[0].Id);
        Assert.Throws<InvalidOperationException>(() => game.RecordRoundScores(active.Id, []));
        game.Cancel();

        var scoredGame = DomainTestData.CreateGame();
        var round = DomainTestData.CompleteRound(scoredGame);
        var id = scoredGame.Players[0].Id;
        Assert.Throws<InvalidOperationException>(() =>
            scoredGame.RecordRoundScores(round.Id, [new PlayerScore(Guid.NewGuid(), 1)]));
        Assert.Throws<InvalidOperationException>(() =>
            scoredGame.RecordRoundScores(round.Id, [new PlayerScore(id, 1), new PlayerScore(id, 2)]));
        Assert.Throws<InvalidOperationException>(() =>
            scoredGame.RecordRoundScores(Guid.NewGuid(), [new PlayerScore(id, 1)]));
        scoredGame.RecordRoundScores(round.Id, [new PlayerScore(id, 1)]);
        Assert.Equal(1, scoredGame.Scores[id].Points);
    }

    [Fact]
    public void OverflowCannotPartiallyAwardPointsOrConsumeTheRound()
    {
        var game = DomainTestData.CreateGame();
        var first = DomainTestData.CompleteRound(game);
        var alice = game.Players[0].Id;
        var bob = game.Players[1].Id;
        game.RecordRoundScores(first.Id, [new PlayerScore(alice, int.MaxValue)]);
        var second = DomainTestData.CompleteRound(game);

        Assert.Throws<OverflowException>(() =>
            game.RecordRoundScores(second.Id, [new PlayerScore(bob, 1), new PlayerScore(alice, 1)]));
        Assert.Equal(0, game.Scores[bob].Points);
        Assert.Equal(int.MaxValue, game.Scores[alice].Points);
        game.RecordRoundScores(second.Id, [new PlayerScore(bob, 1)]);
        Assert.Equal(1, game.Scores[bob].Points);
    }
}
