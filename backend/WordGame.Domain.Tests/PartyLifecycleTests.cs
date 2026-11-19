namespace WordGame.Domain.Tests;

public sealed class PartyLifecycleTests
{
    [Fact]
    public void CountdownUsesOneServerDeadlineAndCannotCompleteEarly()
    {
        var lobby = Ready();
        var at = DomainTestData.StartedAt.AddSeconds(3);
        var starting = lobby.BeginStart(lobby.HostPlayerId, at);
        Assert.Equal(at, starting.StartsAt);
        Assert.Throws<LobbyActionException>(() => starting.CompleteStart(Guid.NewGuid(), at.AddTicks(-1)));
        Assert.Equal(LobbyStatus.Started, starting.CompleteStart(Guid.NewGuid(), at).Status);
        Assert.Null(starting.AbortStart().StartsAt);
    }

    [Fact]
    public void RematchPreservesIdentitySettingsAndResetsReadinessAndCurrentGame()
    {
        var lobby = Ready();
        var gameId = Guid.NewGuid();
        var active = lobby.BeginStart(lobby.HostPlayerId).CompleteStart(gameId);
        var ended = active.FinishMatch(gameId, null, DomainTestData.StartedAt.AddMinutes(40), TimeSpan.FromMinutes(30));
        Assert.Equal(LobbyStatus.Completed, ended.Status);
        Assert.Equal(DomainTestData.StartedAt.AddMinutes(70), ended.ExpiresAt);
        Assert.Same(active, active.FinishMatch(Guid.NewGuid(), null, DomainTestData.StartedAt, TimeSpan.FromMinutes(30)));
        Assert.True(Assert.Throws<LobbyActionException>(() => ended.PrepareRematch(ended.Players[1].Id)).Forbidden);
        var waiting = ended.PrepareRematch(ended.HostPlayerId);
        Assert.Equal(LobbyStatus.Waiting, waiting.Status);
        Assert.Null(waiting.GameId);
        Assert.Null(waiting.StartsAt);
        Assert.Same(lobby.Settings, waiting.Settings);
        Assert.Equal(lobby.Players, waiting.Players);
        Assert.All(waiting.Roster.Members.Values, member => Assert.False(member.IsReady));
        Assert.Throws<LobbyActionException>(() => waiting.BeginStart(waiting.HostPlayerId));
        var declined = ended.Leave(ended.HostPlayerId);
        Assert.Single(declined.Players);
        Assert.Equal(declined.Players[0].Id, declined.HostPlayerId);
        Assert.Throws<LobbyActionException>(() => declined.PrepareRematch(declined.HostPlayerId).BeginStart(declined.HostPlayerId));
    }

    private static Lobby Ready()
    {
        var at = DomainTestData.StartedAt;
        var lobby = new Lobby(Guid.NewGuid(), new LobbyCode("ABC234"), new Player(Guid.NewGuid(), "Host"), at, at.AddMinutes(30))
            .Join(new Player(Guid.NewGuid(), "Bob"));
        foreach (var player in lobby.Players)
        {
            lobby = lobby.Connect(player.Id, Guid.NewGuid()).SetReady(player.Id, true);
        }

        return lobby;
    }
}
