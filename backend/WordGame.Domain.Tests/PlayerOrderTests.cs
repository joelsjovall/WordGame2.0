namespace WordGame.Domain.Tests;

public sealed class PlayerOrderTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void NextPlayerFollowsRosterOrderAndWrapsForBothModes(int playerCount)
    {
        var game = DomainTestData.CreateGame(playerCount);
        var current = game.Players[0].Id;

        for (var index = 1; index <= playerCount; index++)
        {
            current = game.GetNextPlayerId(current);
            Assert.Equal(game.Players[index % playerCount].Id, current);
        }

        Assert.Equal(GameStatus.WaitingToStart, game.Status);
        Assert.Null(game.CurrentRound);
    }

    [Fact]
    public void UnknownPlayerIsRejectedInsteadOfSilentlySelectingTheFirstPlayer()
    {
        var game = DomainTestData.CreateGame();
        Assert.Throws<ArgumentException>(() => game.GetNextPlayerId(Guid.NewGuid()));
        Assert.Throws<ArgumentException>(() => game.GetNextPlayerId(Guid.Empty));
        Assert.Equal(GameStatus.WaitingToStart, game.Status);
    }

    [Fact]
    public void LookupUsesRosterSequenceRatherThanNamesOrGuidSorting()
    {
        var game = new Game(Guid.NewGuid(),
        [
            new Player(Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"), "Zoe"),
            new Player(Guid.Parse("00000000-0000-0000-0000-000000000001"), "Amy"),
            new Player(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "Ben")
        ]);

        Assert.Equal(game.Players[1].Id, game.GetNextPlayerId(game.Players[0].Id));
        Assert.Equal(game.Players[2].Id, game.GetNextPlayerId(game.Players[1].Id));
        Assert.Equal(game.Players[0].Id, game.GetNextPlayerId(game.Players[2].Id));
    }

    [Fact]
    public void RosterLookupCanDriveTheExistingRoundWithoutMutatingOtherGames()
    {
        var game = DomainTestData.CreateGame();
        var other = DomainTestData.CreateGame();
        var round = game.StartRound(Guid.NewGuid(), game.Players[0].Id);
        var next = game.GetNextPlayerId(round.Turn.PlayerId);

        Assert.Equal(game.Players[0].Id, round.Turn.PlayerId);
        round.AssignTurn(next);

        Assert.Equal(game.Players[1].Id, round.Turn.PlayerId);
        Assert.Null(other.CurrentRound);
        Assert.Throws<ArgumentException>(() => other.GetNextPlayerId(next));
    }
}
