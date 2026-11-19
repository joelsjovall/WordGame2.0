using WordGame.Application.Lobbies;
using WordGame.Domain;
using WordGame.Infrastructure.Sessions;

namespace WordGame.Infrastructure.Tests;

public sealed class LobbyPlayerStoreTests
{
    [Fact]
    public async Task GraceCleanupMigratesHostAndOriginalCreationReceiptCannotLeakTheLobby()
    {
        var clock = new ManualSessionClock();
        var store = new InMemoryLobbySessionStore(clock, new LobbyRules(TimeSpan.FromMinutes(30), startCountdown: TimeSpan.Zero));
        var host = new Player(Guid.NewGuid(), "Host");
        var requestId = Guid.NewGuid();
        var created = (await store.CreateAsync(host, requestId)).Lobby;
        var bob = new Player(Guid.NewGuid(), "Bob");
        await store.JoinAsync(created.Code, bob);
        await store.UpdateAsync(created.Id, current => current.Connect(bob.Id, Guid.NewGuid()));
        clock.Advance(TimeSpan.FromMinutes(2) - TimeSpan.FromTicks(1));
        Assert.Equal(host.Id, (await store.GetAsync(created.Id))!.HostPlayerId);
        clock.Advance(TimeSpan.FromTicks(1));
        Assert.Equal(0, await store.CleanupAsync());
        var migrated = (await store.GetAsync(created.Id))!;
        Assert.Equal(bob.Id, migrated.HostPlayerId);
        Assert.Single(migrated.Players);
        await Assert.ThrowsAsync<LobbyCreationConflictException>(() => store.CreateAsync(host, requestId));
        await store.JoinAsync(created.Code, host);
        Assert.Equal(bob.Id, (await store.CreateAsync(host, requestId)).Lobby.HostPlayerId);
        await store.UpdateAsync(created.Id, current => current.RemovePlayer(bob.Id, host.Id));
        await Assert.ThrowsAsync<LobbyActionException>(() => store.JoinAsync(created.Code, host));
        Assert.Equal(LobbyStatus.Closed, (await store.UpdateAsync(created.Id, current => current.Leave(bob.Id)))!.Status);
        Assert.Null(await store.GetAsync(created.Id));
        Assert.Null(await store.JoinAsync(created.Code, host));
    }

    [Fact]
    public async Task DisconnectDuringStartupPreservesCurrentPresenceAndRollsBackWithoutCommittingTheGame()
    {
        var clock = new ManualSessionClock();
        var store = new InMemoryLobbySessionStore(clock, new LobbyRules(TimeSpan.FromMinutes(30), startCountdown: TimeSpan.Zero));
        var host = new Player(Guid.NewGuid(), "Host");
        var lobby = (await store.CreateAsync(host, Guid.NewGuid())).Lobby;
        lobby = (await store.JoinAsync(lobby.Code, new Player(Guid.NewGuid(), "Bob")))!;
        foreach (var player in lobby.Players)
        {
            lobby = (await store.UpdateAsync(lobby.Id, current => current.Connect(player.Id, Guid.NewGuid()).SetReady(player.Id, true)))!;
        }

        var release = new TaskCompletionSource<Guid>(TaskCreationOptions.RunContinuationsAsynchronously);
        var starting = store.StartAsync(lobby.Id, host.Id, lobby.Version, _ => release.Task);
        var connection = Assert.Single(lobby.Roster.Members[host.Id].Connections);
        await store.UpdateAsync(lobby.Id, current => current.Disconnect(host.Id, connection, clock.GetUtcNow()));
        release.SetResult(Guid.NewGuid());
        await Assert.ThrowsAsync<LobbyActionException>(() => starting);
        var restored = (await store.GetAsync(lobby.Id))!;
        Assert.Null(restored.GameId);
        Assert.Equal(LobbyStatus.Waiting, restored.Status);
        Assert.False(restored.Roster.Members[host.Id].IsConnected);
        Assert.All(restored.Roster.Members.Values, member => Assert.False(member.IsReady));
    }
}
