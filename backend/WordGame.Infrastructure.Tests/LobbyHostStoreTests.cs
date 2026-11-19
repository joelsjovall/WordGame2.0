using WordGame.Application.Lobbies;
using WordGame.Domain;
using WordGame.Infrastructure.Sessions;

namespace WordGame.Infrastructure.Tests;

public sealed class LobbyHostStoreTests
{
    [Fact]
    public async Task WatchersGetInitialAndLatestStateAndCannotMissSubscriptionRace()
    {
        var clock = new ManualSessionClock();
        var store = new InMemoryLobbySessionStore(clock, new LobbyRules(TimeSpan.FromMinutes(30), startCountdown: TimeSpan.Zero));
        var host = new Player(Guid.NewGuid(), "Host");
        var lobby = (await store.CreateAsync(host, Guid.NewGuid())).Lobby;
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var watch = store.WatchAsync(lobby.Id, cancellation.Token).GetAsyncEnumerator();
        Assert.True(await watch.MoveNextAsync());
        Assert.Equal(0, watch.Current!.Version);
        var next = watch.MoveNextAsync().AsTask();
        var changed = await store.ConfigureAsync(lobby.Id, host.Id, 0, new LobbySettings(3, 10, 90));
        Assert.True(await next);
        Assert.Same(changed, watch.Current);
        await Assert.ThrowsAsync<LobbyActionException>(() => store.ConfigureAsync(lobby.Id, host.Id, 0, new()));
        clock.Advance(TimeSpan.FromMinutes(30));
        await store.CleanupAsync();
        Assert.True(await watch.MoveNextAsync());
        Assert.Null(watch.Current);
        Assert.False(await watch.MoveNextAsync());
    }

    [Fact]
    public async Task StartupSerializesRacesSurvivesExpiryAndReplaysOneGame()
    {
        var clock = new ManualSessionClock();
        var store = new InMemoryLobbySessionStore(clock, new LobbyRules(TimeSpan.FromMinutes(30), startCountdown: TimeSpan.Zero));
        var host = new Player(Guid.NewGuid(), "Host");
        var lobby = (await store.CreateAsync(host, Guid.NewGuid())).Lobby;
        lobby = (await store.JoinAsync(lobby.Code, new Player(Guid.NewGuid(), "Bob")))!;
        lobby = await Ready(store, lobby);
        var reservation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var game = new TaskCompletionSource<Guid>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        async Task<Guid> Create(Lobby frozen) { calls++; Assert.Equal(2, frozen.Players.Count); reservation.SetResult(); return await game.Task; }
        var starting = store.StartAsync(lobby.Id, host.Id, lobby.Version, Create);
        await reservation.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.ThrowsAsync<LobbyActionException>(() => store.StartAsync(lobby.Id, host.Id, lobby.Version, Create));
        var reserved = (await store.GetAsync(lobby.Id))!;
        await Assert.ThrowsAsync<LobbyActionException>(() => store.ConfigureAsync(lobby.Id, host.Id, reserved.Version, new()));
        await Assert.ThrowsAsync<LobbyActionException>(() => store.JoinAsync(lobby.Code, new Player(Guid.NewGuid(), "Carol")));
        clock.Advance(TimeSpan.FromMinutes(30));
        Assert.Equal(0, await store.CleanupAsync());
        var id = Guid.NewGuid(); game.SetResult(id);
        Assert.Equal(id, (await starting)!.GameId);
        Assert.Equal(1, calls);
        // Active matches retain the lobby beyond its original waiting deadline.
        Assert.NotNull(await store.GetAsync(lobby.Id));
        await store.UpdateAsync(lobby.Id, current => current.FinishMatch(id, null, clock.GetUtcNow(), TimeSpan.FromMinutes(30)));
        clock.Advance(TimeSpan.FromMinutes(30));
        Assert.Null(await store.GetAsync(lobby.Id));
    }

    [Fact]
    public async Task FailedStartupRestoresWaitingWithANewVersionAndNoPartialSettings()
    {
        var store = new InMemoryLobbySessionStore(new ManualSessionClock(), new LobbyRules(TimeSpan.FromMinutes(30), startCountdown: TimeSpan.Zero));
        var host = new Player(Guid.NewGuid(), "Host");
        var lobby = (await store.CreateAsync(host, Guid.NewGuid())).Lobby;
        lobby = (await store.JoinAsync(lobby.Code, new Player(Guid.NewGuid(), "Bob")))!;
        lobby = await Ready(store, lobby);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.StartAsync(lobby.Id, host.Id, lobby.Version,
            _ => Task.FromException<Guid>(new InvalidOperationException("Test failure"))));
        var restored = (await store.GetAsync(lobby.Id))!;
        Assert.Equal(LobbyStatus.Waiting, restored.Status);
        Assert.Null(restored.GameId);
        Assert.Same(lobby.Settings, restored.Settings);
        Assert.Equal(lobby.Version + 2, restored.Version);
    }
    private static async Task<Lobby> Ready(InMemoryLobbySessionStore store, Lobby lobby)
    {
        foreach (var player in lobby.Players)
        {
            lobby = (await store.UpdateAsync(lobby.Id, current => current.Connect(player.Id, Guid.NewGuid()).SetReady(player.Id, true)))!;
        }

        return lobby;
    }
}
