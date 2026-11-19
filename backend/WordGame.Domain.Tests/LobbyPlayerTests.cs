namespace WordGame.Domain.Tests;

public sealed class LobbyPlayerTests
{
    private static readonly DateTimeOffset Now = DomainTestData.StartedAt;
    private static Lobby Room() => new Lobby(Guid.NewGuid(), new LobbyCode("ABC234"), new Player(Guid.NewGuid(), "Host"), Now, Now.AddMinutes(30))
        .Join(new Player(Guid.NewGuid(), "Bob"));

    [Fact]
    public void ReadinessRequiresMembershipAndConnectionAndMultiTabDisconnectsAreIndependent()
    {
        var original = Room();
        var host = original.HostPlayerId;
        var first = Guid.NewGuid(); var second = Guid.NewGuid();
        Assert.Throws<LobbyActionException>(() => original.SetReady(host, true));
        Assert.True(Assert.Throws<LobbyActionException>(() => original.SetReady(Guid.NewGuid(), false)).Forbidden);
        var lobby = original.Connect(host, first).SetReady(host, true).Connect(host, second);
        Assert.True(lobby.Roster.Members[host].IsReady);
        lobby = lobby.Disconnect(host, first, Now);
        Assert.True(lobby.Roster.Members[host].IsReady);
        Assert.Null(lobby.Roster.Members[host].DisconnectedAt);
        lobby = lobby.Disconnect(host, second, Now);
        Assert.False(lobby.Roster.Members[host].IsReady);
        Assert.Equal(Now, lobby.Roster.Members[host].DisconnectedAt);
        var reconnect = lobby.Connect(host, Guid.NewGuid());
        Assert.Equal(2, reconnect.Players.Count);
        Assert.Equal(host, reconnect.HostPlayerId);
        Assert.False(reconnect.Roster.Members[host].IsReady);
        Assert.False(original.Roster.Members[host].IsConnected);
    }

    [Fact]
    public void MembershipAndSettingsChangesInvalidateReadinessAndKicksRevokeRejoining()
    {
        var lobby = Ready(Room());
        Assert.True(lobby.Roster.AllReady);
        var configured = lobby.Configure(lobby.HostPlayerId, new LobbySettings(3, 5));
        Assert.All(configured.Roster.Members.Values, member => Assert.False(member.IsReady));
        var joined = lobby.Join(new Player(Guid.NewGuid(), "Carol"), Now);
        Assert.All(joined.Roster.Members.Values, member => Assert.False(member.IsReady));
        var bob = lobby.Players[1];
        Assert.True(Assert.Throws<LobbyActionException>(() => lobby.RemovePlayer(bob.Id, lobby.HostPlayerId)).Forbidden);
        Assert.Throws<LobbyActionException>(() => lobby.RemovePlayer(lobby.HostPlayerId, lobby.HostPlayerId));
        var removed = lobby.RemovePlayer(lobby.HostPlayerId, bob.Id);
        Assert.Single(removed.Players);
        Assert.True(Assert.Throws<LobbyActionException>(() => removed.Join(bob)).Forbidden);
        Assert.True(Assert.Throws<LobbyActionException>(() => removed.SetReady(bob.Id, true)).Forbidden);
        Assert.True(Assert.Throws<LobbyActionException>(() => removed.Connect(bob.Id, Guid.NewGuid())).Forbidden);
        Assert.True(Assert.Throws<LobbyActionException>(() => removed.Leave(bob.Id)).Forbidden);
    }

    [Fact]
    public void HostMigrationPrefersConnectedMembersAndGraceExpiryIsExact()
    {
        var lobby = Room().Join(new Player(Guid.NewGuid(), "Carol"), Now);
        var host = lobby.HostPlayerId;
        var carol = lobby.Players[2].Id;
        lobby = lobby.Connect(carol, Guid.NewGuid());
        var leaving = lobby.Leave(host);
        Assert.Equal(carol, leaving.HostPlayerId);
        Assert.All(leaving.Roster.Members.Values, member => Assert.False(member.IsReady));
        Assert.Equal(host, lobby.PruneDisconnected(Now.AddMinutes(2).AddTicks(-1), TimeSpan.FromMinutes(2)).HostPlayerId);
        var expired = lobby.PruneDisconnected(Now.AddMinutes(2), TimeSpan.FromMinutes(2));
        Assert.Equal(carol, expired.HostPlayerId);
        Assert.Single(expired.Players);
        Assert.Equal(LobbyStatus.Closed, expired.Leave(carol).Status);
    }

    [Fact]
    public void StartupRequiresEveryoneReadyAndConnectedAndRejectsExplicitDepartureDuringReservation()
    {
        var lobby = Room();
        Assert.Throws<LobbyActionException>(() => lobby.BeginStart(lobby.HostPlayerId));
        lobby = Ready(lobby);
        var starting = lobby.BeginStart(lobby.HostPlayerId);
        Assert.Throws<LobbyActionException>(() => starting.Leave(starting.Players[1].Id));
        Assert.Throws<LobbyActionException>(() => starting.RemovePlayer(starting.HostPlayerId, starting.Players[1].Id));
        var host = starting.HostPlayerId;
        starting = starting.Disconnect(host, Assert.Single(starting.Roster.Members[host].Connections), Now);
        Assert.Throws<LobbyActionException>(() => starting.CompleteStart(Guid.NewGuid()));
        var waiting = starting.AbortStart();
        Assert.Equal(LobbyStatus.Waiting, waiting.Status);
        Assert.False(waiting.Roster.Members[host].IsConnected);
        Assert.All(waiting.Roster.Members.Values, member => Assert.False(member.IsReady));
    }

    private static Lobby Ready(Lobby lobby)
    {
        foreach (var player in lobby.Players)
        {
            lobby = lobby.Connect(player.Id, Guid.NewGuid()).SetReady(player.Id, true);
        }

        return lobby;
    }
}
