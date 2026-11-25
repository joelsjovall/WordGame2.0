namespace WordGame.Domain.Tests;

public sealed class MatchCompletionTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void PartyDefaultsToTwoRoundsPerPlayer(int players)
    {
        var game = DomainTestData.CreateGame(players);
        Assert.Equal(MatchFormat.Party, game.Settings.Format);
        Assert.Equal(players * 2, game.Settings.RoundCount);
        Assert.Same(game.Settings, game.Copy().Settings);
        Assert.Null(game.Result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void PartyLengthMustBePositive(int rounds) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => MatchSettings.Party(rounds));

    [Fact]
    public void OnlineHasFixedSixRoundLengthAndRequiresTwoPlayers()
    {
        var settings = MatchSettings.Online();
        var game = new Game(Guid.NewGuid(), DomainTestData.CreateGame().Players, settings);
        Assert.Equal(6, game.Settings.RoundCount);
        Assert.Equal(MatchFormat.Online, game.Settings.Format);
        Assert.Throws<ArgumentException>(() => new Game(Guid.NewGuid(), DomainTestData.CreateGame(3).Players, settings));
        Assert.Throws<ArgumentException>(() => new Game(Guid.NewGuid(), DomainTestData.CreateGame(4).Players, settings));
    }

    [Theory]
    [InlineData(50, 50, 20, 0, true)]
    [InlineData(0, 0, 0, 0, true)]
    [InlineData(10, 30, 20, 0, false)]
    [InlineData(10, 0, 0, 0, false)]
    public void StandingsSortScoresAndShareRanksWithoutArbitraryTiebreakers(int a, int b, int c, int d, bool tied)
    {
        var game = new Game(Guid.NewGuid(), DomainTestData.CreateGame(4).Players, MatchSettings.Party(1));
        var values = new[] { a, b, c, d };
        var round = DomainTestData.CompleteRound(game);
        game.RecordRoundScores(round.Id, game.Players.Select((player, index) => new PlayerScore(player.Id, values[index])));
        game.Complete();
        var result = Assert.IsType<MatchResult>(game.Result);
        Assert.Equal(game.Id, result.GameId);
        Assert.Equal(1, result.CompletedRoundCount);
        Assert.Equal(tied, result.IsTie);
        Assert.Equal(values.OrderDescending(), result.Standings.Select(standing => standing.Score));
        foreach (var standing in result.Standings)
        {
            Assert.Equal(1 + values.Count(value => value > standing.Score), standing.Rank);
            Assert.Equal(game.Players.Single(player => player.Id == standing.PlayerId).DisplayName, standing.DisplayName);
        }
        Assert.Equal(game.Players.Where((_, index) => values[index] == values.Max()).Select(player => player.Id), result.WinnerIds);
        Assert.Same(result, game.Copy().Result);
        Assert.Throws<NotSupportedException>(() => ((IList<MatchStanding>)result.Standings).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<Guid>)result.WinnerIds).Clear());
        Assert.Throws<InvalidOperationException>(() => game.Complete());
        Assert.Throws<InvalidOperationException>(() => game.Cancel());
        Assert.Throws<InvalidOperationException>(() => game.StartRound(Guid.NewGuid(), game.Players[0].Id));
    }

    [Fact]
    public void CompletionRequiresAllConfiguredRoundsAndTheirScoresAndCannotExceedLimit()
    {
        var game = new Game(Guid.NewGuid(), DomainTestData.CreateGame().Players, MatchSettings.Party(2));
        var first = DomainTestData.CompleteRound(game);
        game.RecordRoundScores(first.Id, []);
        Assert.Throws<InvalidOperationException>(() => game.Complete());
        var second = DomainTestData.CompleteRound(game);
        Assert.Throws<InvalidOperationException>(() => game.Complete());
        Assert.Null(game.Result);
        Assert.Throws<InvalidOperationException>(() => game.StartRound(Guid.NewGuid(), game.Players[0].Id));
        game.RecordRoundScores(second.Id, []);
        game.Complete();
        Assert.Equal(GameStatus.Completed, game.Status);
        Assert.Equal(2, game.Result!.WinnerIds.Count);
    }

    [Fact]
    public void ResultHighlightsTheLargestCaughtBluff()
    {
        var game = new Game(Guid.NewGuid(), DomainTestData.CreateGame().Players, MatchSettings.Party(2));
        var first = DomainTestData.CompleteRound(game);
        game.RecordRoundScores(first.Id, []);
        var second = game.StartRound(Guid.NewGuid(), game.Players[0].Id);
        second.SelectCategory(game.Players[0].Id, new Category("capitals", "Capitals", CategoryDifficulty.Easy));
        second.RecordBid(new Bid(game.Players[0].Id, 5));
        second.AssignTurn(game.Players[1].Id);
        var challenge = DomainTestData.BeginChallenge(game, second);
        challenge.Resolve(ChallengeOutcome.Failed);
        second.Complete();
        game.RecordRoundScores(second.Id, []);

        game.Complete();

        Assert.Equal(new BluffHighlight("Capitals", 5, game.Players[0].DisplayName,
            game.Players[1].DisplayName), game.Result!.BestBluffCall);
        Assert.Equal(game.Result.BestBluffCall, game.Copy().Result!.BestBluffCall);
    }
}
