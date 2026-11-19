namespace WordGame.Domain.Tests;

public sealed class LobbySettingsTests
{
    [Theory]
    [InlineData(1, 4, 30)]
    [InlineData(5, 4, 30)]
    [InlineData(4, 0, 30)]
    [InlineData(4, 101, 30)]
    [InlineData(4, 4, 4)]
    [InlineData(4, 4, 301)]
    public void SettingsRejectOutOfRangeValues(int players, int rounds, int seconds) =>
        Assert.Throws<ArgumentException>(() => new LobbySettings(players, rounds, seconds));

    [Fact]
    public void SettingsAreImmutableAndFreezeAutomaticRoundsAtStart()
    {
        var difficulties = new[] { CategoryDifficulty.Hard };
        var settings = new LobbySettings(2, null, 5, difficulties);
        difficulties[0] = CategoryDifficulty.Easy;
        Assert.Equal(CategoryDifficulty.Hard, Assert.Single(settings.AllowedDifficulties));
        Assert.Equal(4, settings.ForMatch(2).RoundCount);
        Assert.Equal(TimeSpan.FromSeconds(5), settings.ForMatch(2).ChallengeDuration);
        Assert.Throws<ArgumentException>(() => new LobbySettings(allowedDifficulties: []));
        Assert.Throws<ArgumentException>(() => new LobbySettings(allowedDifficulties: [CategoryDifficulty.Easy, CategoryDifficulty.Easy]));
        Assert.Throws<ArgumentException>(() => new LobbySettings(allowedDifficulties: [(CategoryDifficulty)99]));
    }

    [Fact]
    public void OnlyHostCanConfigureOrStartAndStartedSettingsCannotChange()
    {
        var host = new Player(Guid.NewGuid(), "Host");
        var lobby = new Lobby(Guid.NewGuid(), new LobbyCode("ABC234"), host, DomainTestData.StartedAt, DomainTestData.StartedAt.AddMinutes(30));
        Assert.True(Assert.Throws<LobbyActionException>(() => lobby.Configure(Guid.NewGuid(), new())).Forbidden);
        Assert.True(Assert.Throws<LobbyActionException>(() => lobby.BeginStart(Guid.NewGuid())).Forbidden);
        Assert.Throws<LobbyActionException>(() => lobby.BeginStart(host.Id));
        var configured = lobby.Configure(host.Id, new LobbySettings(2, 7, 90, [CategoryDifficulty.Hard]));
        var joined = configured.Join(new Player(Guid.NewGuid(), "Bob"));
        Assert.Equal(LobbyJoinFailure.Full, Assert.Throws<LobbyJoinException>(() => joined.Join(new Player(Guid.NewGuid(), "Carol"))).Reason);
        foreach (var player in joined.Players)
        {
            joined = joined.Connect(player.Id, Guid.NewGuid()).SetReady(player.Id, true);
        }

        var starting = joined.BeginStart(host.Id);
        Assert.Throws<LobbyActionException>(() => starting.Configure(host.Id, new()));
        Assert.Throws<LobbyActionException>(() => starting.Join(new Player(Guid.NewGuid(), "Carol")));
        var started = starting.CompleteStart(Guid.NewGuid());
        Assert.Equal(LobbyStatus.Started, started.Status);
        Assert.Same(configured.Settings, started.Settings);
        Assert.Throws<LobbyActionException>(() => started.BeginStart(host.Id));
        Assert.Throws<LobbyActionException>(() => started.Configure(host.Id, new()));
        Assert.Equal(LobbyStatus.Waiting, starting.AbortStart().Status);
        Assert.Equal(LobbyStatus.Waiting, lobby.Status);
        Assert.Equal(0, lobby.Version);
        var three = lobby.Join(new Player(Guid.NewGuid(), "Bob")).Join(new Player(Guid.NewGuid(), "Carol"));
        Assert.Throws<LobbyActionException>(() => three.Configure(host.Id, new LobbySettings(2)));
    }
}
