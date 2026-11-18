using WordGame.Application.Sessions;
using WordGame.Domain;

namespace WordGame.Infrastructure.Tests;

public sealed class GameSessionStoreTests
{
    [Fact]
    public async Task LifecycleGuardsRejectEarlyCompletionAndTerminalRestarts()
    {
        var store = SessionTestData.Store(new ManualSessionClock());
        var created = await store.CreateAsync(SessionTestData.Players());
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.UpdateAsync(created.Id, session => session.Activate()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.UpdateAsync(created.Id, session => session.Complete()));
        await store.UpdateAsync(created.Id, SessionTestData.Activate);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.UpdateAsync(created.Id, session => session.Complete()));
        var completed = await store.CreateAsync(SessionTestData.Players());
        await store.UpdateAsync(completed.Id, SessionTestData.Complete);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.UpdateAsync(completed.Id, session => session.BeginStarting()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.UpdateAsync(completed.Id, session => session.Abandon()));
        await store.UpdateAsync(created.Id, session => session.Abandon());
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.UpdateAsync(created.Id,
            session => session.Connect(created.Game.Players[0].Id, Guid.NewGuid())));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task SharedInterfaceCreatesIndependentSessionsForBothModes(int playerCount)
    {
        IGameSessionStore store = SessionTestData.Store(new ManualSessionClock());
        var roster = SessionTestData.Players(playerCount);
        var first = await store.CreateAsync(roster);
        var second = await store.CreateAsync(roster);
        await store.UpdateAsync(first.Id, session => session.BeginStarting());

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(playerCount, first.Game.Players.Count);
        Assert.Equal(SessionStatus.Starting, (await store.GetAsync(first.Id))!.Status);
        Assert.Equal(SessionStatus.Waiting, (await store.GetAsync(second.Id))!.Status);
        Assert.Null(await store.GetAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task ReadAndCallbackReferencesCannotMutateStoredState()
    {
        var store = SessionTestData.Store(new ManualSessionClock());
        var created = await store.CreateAsync(SessionTestData.Players());
        created.Game.Cancel();
        Assert.Equal(GameStatus.WaitingToStart, (await store.GetAsync(created.Id))!.Game.Status);
        GameSession? captured = null;
        var updated = await store.UpdateAsync(created.Id, session =>
        {
            captured = session;
            session.BeginStarting();
        });
        captured!.Abandon();
        updated.Game.Cancel();

        var stored = await store.GetAsync(created.Id);
        Assert.Equal(SessionStatus.Starting, stored!.Status);
        Assert.Equal(GameStatus.WaitingToStart, stored.Game.Status);
        Assert.Equal(1, stored.Version);
    }

    [Fact]
    public async Task ThrowingCancelledAndInconsistentTransactionsDoNotCommit()
    {
        var store = SessionTestData.Store(new ManualSessionClock());
        var created = await store.CreateAsync(SessionTestData.Players());
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.UpdateAsync(created.Id, session =>
        {
            session.BeginStarting();
            throw new InvalidOperationException("Rejected action");
        }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.UpdateAsync(created.Id, session =>
            session.Game.StartRound(Guid.NewGuid(), session.Game.Players[0].Id)));
        using var cancellation = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.UpdateAsync(created.Id, session =>
        {
            session.BeginStarting();
            cancellation.Cancel();
        }, cancellation.Token));

        var stored = await store.GetAsync(created.Id);
        Assert.Equal(SessionStatus.Waiting, stored!.Status);
        Assert.Equal(0, stored.Version);
        Assert.Empty(stored.Game.Rounds);
    }

    [Fact]
    public async Task ConcurrentPresenceUpdatesAreSerializedWithoutLostChanges()
    {
        var store = SessionTestData.Store(new ManualSessionClock());
        var created = await store.CreateAsync(SessionTestData.Players());
        var player = created.Game.Players[0].Id;
        await Task.WhenAll(Enumerable.Range(0, 50).Select(_ =>
            Task.Run(() => store.UpdateAsync(created.Id, session => session.Connect(player, Guid.NewGuid())))));

        var stored = await store.GetAsync(created.Id);
        Assert.Equal(50, stored!.Version);
        Assert.Equal(50, stored.Players.Single(presence => presence.PlayerId == player).ConnectionCount);
    }

    [Fact]
    public async Task CompetingEqualBidsCommitOnlyOnce()
    {
        var store = SessionTestData.Store(new ManualSessionClock());
        var created = await store.CreateAsync(SessionTestData.Players());
        var bidder = created.Game.Players[0].Id;
        await store.UpdateAsync(created.Id, session =>
        {
            SessionTestData.Activate(session);
            session.Game.CurrentRound!.SelectCategory(bidder, new Category("cars", "Cars", CategoryDifficulty.Easy));
            session.Game.CurrentRound.RecordBid(new Bid(bidder, 1));
        });

        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(async () =>
        {
            try
            {
                await store.UpdateAsync(created.Id, session => session.Game.CurrentRound!.RecordBid(new Bid(bidder, 2)));
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        })));

        Assert.Single(results, accepted => accepted);
        var stored = await store.GetAsync(created.Id);
        Assert.Equal(2, stored!.Version);
        Assert.Equal(2, stored.Game.CurrentRound!.HighestBid!.WordCount);
    }

    [Fact]
    public async Task ABlockedSessionDoesNotBlockAnotherAndCancelledWaitersReleaseCleanly()
    {
        var store = SessionTestData.Store(new ManualSessionClock());
        var first = await store.CreateAsync(SessionTestData.Players());
        var second = await store.CreateAsync(SessionTestData.Players());
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var held = Task.Run(() => store.UpdateAsync(first.Id, session =>
        {
            entered.SetResult();
            if (!release.Wait(TimeSpan.FromSeconds(5)))
            {
                throw new TimeoutException("Test failed to release its session update.");
            }
            session.BeginStarting();
        }));
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var other = await store.UpdateAsync(second.Id, session => session.BeginStarting())
                .WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(SessionStatus.Starting, other.Status);
            using var cancellation = new CancellationTokenSource();
            var waiting = store.UpdateAsync(first.Id, session => session.BeginStarting(), cancellation.Token);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        }
        finally
        {
            release.Set();
            await held;
        }
        Assert.Equal(1, (await store.GetAsync(first.Id))!.Version);
    }

    [Fact]
    public async Task FullLifecycleAndReconnectPreserveIdentityAndGameState()
    {
        var clock = new ManualSessionClock();
        var store = SessionTestData.Store(clock);
        var created = await store.CreateAsync(SessionTestData.Players());
        var player = created.Game.Players[0].Id;
        var oldConnection = Guid.NewGuid();
        var newConnection = Guid.NewGuid();
        await store.UpdateAsync(created.Id, session =>
        {
            session.Connect(player, oldConnection);
            SessionTestData.Activate(session);
        });
        await store.UpdateAsync(created.Id, session => session.Disconnect(player, oldConnection));
        clock.Advance(TimeSpan.FromMinutes(1));
        await store.UpdateAsync(created.Id, session => session.Connect(player, newConnection));
        await store.UpdateAsync(created.Id, session => session.Disconnect(player, oldConnection));
        Assert.Equal(0, await store.CleanupAsync());

        var stored = await store.GetAsync(created.Id);
        Assert.Equal(SessionStatus.Active, stored!.Status);
        Assert.Equal(1, stored.Players.Single(presence => presence.PlayerId == player).ConnectionCount);
        Assert.Null(stored.Players.Single(presence => presence.PlayerId == player).DisconnectedAt);
        Assert.Single(stored.Game.Rounds);
        Assert.Equal(created.Game.Players, stored.Game.Players);
    }

    [Fact]
    public async Task PresenceRejectsUnknownPlayersAndConnectionReuseAcrossIdentities()
    {
        var store = SessionTestData.Store(new ManualSessionClock());
        var created = await store.CreateAsync(SessionTestData.Players());
        var id = Guid.NewGuid();
        await store.UpdateAsync(created.Id, session => session.Connect(created.Game.Players[0].Id, id));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.UpdateAsync(created.Id, session => session.Connect(Guid.NewGuid(), Guid.NewGuid())));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.UpdateAsync(created.Id, session => session.Connect(created.Game.Players[1].Id, id)));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.UpdateAsync(created.Id, session => session.Connect(created.Game.Players[0].Id, Guid.Empty)));
        Assert.Equal(1, (await store.GetAsync(created.Id))!.Version);
    }
}
