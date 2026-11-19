using WordGame.Application.Lobbies;
using WordGame.Domain;
using WordGame.Infrastructure.Sessions;

namespace WordGame.Infrastructure.Tests;

public sealed class LobbySessionStoreTests
{
    [Fact]
    public async Task CompetingJoinsCannotExceedCapacityAndSamePlayerReplaysUseOneSlot()
    {
        var store = Store();
        var created = (await store.CreateAsync(new Player(Guid.NewGuid(), "Host"), Guid.NewGuid())).Lobby;
        var bob = new Player(Guid.NewGuid(), "Bob");
        var replays = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(() => store.JoinAsync(created.Code, bob))));
        Assert.All(replays, lobby => Assert.Equal(2, lobby!.Players.Count));
        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(index => Task.Run(async () =>
        {
            try { await store.JoinAsync(created.Code, new Player(Guid.NewGuid(), $"Player {index}")); return true; }
            catch (LobbyJoinException exception) when (exception.Reason == LobbyJoinFailure.Full) { return false; }
        })));
        Assert.Equal(2, results.Count(joined => joined));
        Assert.Equal(4, (await store.GetAsync(created.Id))!.Players.Count);
        Assert.Single(created.Players);
        Assert.Equal(4, (await store.JoinAsync(created.Code, bob))!.Players.Count);
    }

    [Fact]
    public async Task JoinsEnforceExpiryAndInvitationIdentityEvenAfterCodeReuse()
    {
        var clock = new ManualSessionClock();
        var store = new InMemoryLobbySessionStore(clock, new LobbyRules(TimeSpan.FromMinutes(30)), () => new LobbyCode("ABC234"));
        var host = new Player(Guid.NewGuid(), "Host");
        var original = (await store.CreateAsync(host, Guid.NewGuid())).Lobby;
        var player = new Player(Guid.NewGuid(), "Bob");
        Assert.Null(await store.JoinAsync(original.Code, player, Guid.NewGuid()));
        Assert.Null(await store.JoinAsync(new LobbyCode("XYZ567"), player));
        clock.Advance(TimeSpan.FromMinutes(30));
        Assert.Null(await store.JoinAsync(original.Code, player));
        var replacement = (await store.CreateAsync(host, Guid.NewGuid())).Lobby;
        Assert.Null(await store.JoinAsync(original.Code, player, original.Id));
        Assert.Equal(2, (await store.JoinAsync(replacement.Code, player, replacement.Id))!.Players.Count);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.JoinAsync(replacement.Code, new Player(Guid.NewGuid(), "Carol"), cancellationToken: cancellation.Token));
        Assert.Equal(2, (await store.GetAsync(replacement.Id))!.Players.Count);
    }

    [Fact]
    public async Task ACollisionCanBeSkippedWithinTheSameCreation()
    {
        var codes = new Queue<LobbyCode>([new("ABC234"), new("ABC234"), new("XYZ567")]);
        var store = new InMemoryLobbySessionStore(new ManualSessionClock(), new LobbyRules(TimeSpan.FromMinutes(30)), () => codes.Dequeue());
        var host = new Player(Guid.NewGuid(), "Alice");
        var first = await store.CreateAsync(host, Guid.NewGuid());
        var second = await store.CreateAsync(host, Guid.NewGuid());
        Assert.NotEqual(first.Lobby.Code, second.Lobby.Code);
        Assert.Equal("XYZ567", second.Lobby.Code.Value);
    }

    [Fact]
    public async Task CancellationDuringAllocationDoesNotReserveTheCodeOrRequest()
    {
        using var cancellation = new CancellationTokenSource();
        var interrupt = true;
        var store = new InMemoryLobbySessionStore(new ManualSessionClock(), new LobbyRules(TimeSpan.FromMinutes(30)), () =>
        {
            if (interrupt)
            {
                cancellation.Cancel();
            }

            return new LobbyCode("ABC234");
        });
        var host = new Player(Guid.NewGuid(), "Alice");
        var request = Guid.NewGuid();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.CreateAsync(host, request, cancellation.Token));
        interrupt = false;
        Assert.True((await store.CreateAsync(host, request)).WasCreated);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void LobbyLifetimeMustBePositive(int seconds) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new LobbyRules(TimeSpan.FromSeconds(seconds)));

    [Fact]
    public async Task ConcurrentReplaysCreateOneLobbyAndOtherRequestsRemainIndependent()
    {
        var store = Store();
        var host = new Player(Guid.NewGuid(), "Alice");
        var requestId = Guid.NewGuid();
        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(() => store.CreateAsync(host, requestId))));
        Assert.Single(results, result => result.WasCreated);
        var original = results[0].Lobby;
        Assert.All(results, result => Assert.Same(original, result.Lobby));
        await Assert.ThrowsAsync<LobbyCreationConflictException>(() => store.CreateAsync(new Player(host.Id, "Changed"), requestId));
        var other = await store.CreateAsync(host, Guid.NewGuid());
        Assert.NotEqual(original.Id, other.Lobby.Id);
        Assert.NotEqual(original.Code, other.Lobby.Code);
        Assert.Same(original, await store.GetAsync(original.Id));
        Assert.Same(other.Lobby, await store.GetAsync(other.Lobby.Id));
        var differentHost = await store.CreateAsync(new Player(Guid.NewGuid(), "Bob"), requestId);
        Assert.NotEqual(original.Id, differentHost.Lobby.Id);
    }

    [Fact]
    public async Task CodeCollisionsRetryAndFailureDoesNotReserveTheRequest()
    {
        var clock = new ManualSessionClock();
        var code = new LobbyCode("ABC234");
        var store = new InMemoryLobbySessionStore(clock, new LobbyRules(TimeSpan.FromMinutes(30)), () => code);
        var host = new Player(Guid.NewGuid(), "Alice");
        var original = await store.CreateAsync(host, Guid.NewGuid());
        var request = Guid.NewGuid();
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.CreateAsync(host, request));
        Assert.Same(original.Lobby, await store.GetAsync(original.Lobby.Id));
        code = new LobbyCode("XYZ567");
        var retried = await store.CreateAsync(host, request);
        Assert.Equal("XYZ567", retried.Lobby.Code.Value);
        Assert.True(retried.WasCreated);
    }

    [Fact]
    public async Task ManyConcurrentLobbiesHaveUniqueIdsAndUserFriendlyCodes()
    {
        var store = Store();
        var results = await Task.WhenAll(Enumerable.Range(0, 100).Select(index => Task.Run(() =>
            store.CreateAsync(new Player(Guid.NewGuid(), $"Player {index}"), Guid.NewGuid()))));
        Assert.Equal(100, results.Select(result => result.Lobby.Id).Distinct().Count());
        Assert.Equal(100, results.Select(result => result.Lobby.Code).Distinct().Count());
        Assert.All(results, result => Assert.Matches("^[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{6}$", result.Lobby.Code.Value));
    }

    [Fact]
    public async Task ExpiryIsExactAndCleanupReleasesCodesAndIdempotencyEntries()
    {
        var clock = new ManualSessionClock();
        var store = new InMemoryLobbySessionStore(clock, new LobbyRules(TimeSpan.FromMinutes(30)), () => new LobbyCode("ABC234"));
        var host = new Player(Guid.NewGuid(), "Alice");
        var request = Guid.NewGuid();
        var original = await store.CreateAsync(host, request);
        clock.Advance(TimeSpan.FromMinutes(30) - TimeSpan.FromTicks(1));
        Assert.NotNull(await store.GetAsync(original.Lobby.Id));
        Assert.Equal(0, await store.CleanupAsync());
        clock.Advance(TimeSpan.FromTicks(1));
        Assert.Equal(1, await store.CleanupAsync());
        Assert.Null(await store.GetAsync(original.Lobby.Id));
        Assert.Equal(0, await store.CleanupAsync());
        var fresh = await store.CreateAsync(host, request);
        Assert.True(fresh.WasCreated);
        Assert.NotEqual(original.Lobby.Id, fresh.Lobby.Id);
        Assert.Equal(original.Lobby.Code, fresh.Lobby.Code);
        clock.Advance(TimeSpan.FromMinutes(30));
        Assert.Null(await store.GetAsync(fresh.Lobby.Id));
    }

    [Fact]
    public async Task InvalidAndCancelledRequestsDoNotCreateALobby()
    {
        var store = Store();
        var host = new Player(Guid.NewGuid(), "Alice");
        await Assert.ThrowsAsync<ArgumentException>(() => store.CreateAsync(host, Guid.Empty));
        await Assert.ThrowsAsync<ArgumentNullException>(() => store.CreateAsync(null!, Guid.NewGuid()));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var request = Guid.NewGuid();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.CreateAsync(host, request, cancelled.Token));
        Assert.True((await store.CreateAsync(host, request)).WasCreated);
    }

    private static InMemoryLobbySessionStore Store() => new(new ManualSessionClock(), new LobbyRules(TimeSpan.FromMinutes(30)));
}
