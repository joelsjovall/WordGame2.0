using WordGame.Application.Sessions;

namespace WordGame.Infrastructure.Tests;

public sealed class SessionCleanupAndNotificationTests
{
    [Fact]
    public async Task CleanupRacingQueuedUpdatesCannotResurrectARemovedSession()
    {
        var store = SessionTestData.Store(new ManualSessionClock(), TimeSpan.Zero);
        var created = await store.CreateAsync(SessionTestData.Players());
        await store.UpdateAsync(created.Id, session => session.Abandon());
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var held = Task.Run(() => store.UpdateAsync(created.Id, _ =>
        {
            entered.SetResult();
            if (!release.Wait(TimeSpan.FromSeconds(5)))
            {
                throw new TimeoutException("Test failed to release its session update.");
            }
        }));
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var cleanup = store.CleanupAsync();
            var update = store.UpdateAsync(created.Id, _ => { });
            release.Set();
            await held;
            Assert.Equal(1, await cleanup);
            try
            {
                await update;
            }
            catch (KeyNotFoundException)
            {
                // The removal may linearize before this queued update.
            }
            Assert.Null(await store.GetAsync(created.Id));
        }
        finally
        {
            release.Set();
            await held;
        }
    }

    [Fact]
    public async Task MultipleObserversReceiveTheSameCommittedRevision()
    {
        var store = SessionTestData.Store(new ManualSessionClock());
        var created = await store.CreateAsync(SessionTestData.Players());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var first = store.WatchAsync(created.Id, timeout.Token).GetAsyncEnumerator();
        await using var second = store.WatchAsync(created.Id, timeout.Token).GetAsyncEnumerator();
        Assert.True(await first.MoveNextAsync());
        Assert.True(await second.MoveNextAsync());
        await store.UpdateAsync(created.Id, session => session.BeginStarting());
        Assert.True(await first.MoveNextAsync());
        Assert.True(await second.MoveNextAsync());
        Assert.Equal(first.Current, second.Current);
        Assert.Equal(1, first.Current.Version);
    }

    [Fact]
    public async Task AllDisconnectedSessionsAreAbandonedAfterGraceAndRemovedAfterRetention()
    {
        var clock = new ManualSessionClock();
        var store = SessionTestData.Store(clock);
        var created = await store.CreateAsync(SessionTestData.Players());
        clock.Advance(TimeSpan.FromMinutes(2) - TimeSpan.FromTicks(1));
        Assert.Equal(0, await store.CleanupAsync());
        Assert.Equal(SessionStatus.Waiting, (await store.GetAsync(created.Id))!.Status);
        clock.Advance(TimeSpan.FromTicks(1));
        Assert.Equal(0, await store.CleanupAsync());
        var abandoned = await store.GetAsync(created.Id);
        Assert.Equal(SessionStatus.Abandoned, abandoned!.Status);
        Assert.Equal(1, abandoned.Version);
        clock.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal(1, await store.CleanupAsync());
        Assert.Null(await store.GetAsync(created.Id));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => store.UpdateAsync(created.Id, _ => { }));
    }

    [Fact]
    public async Task OneConnectedPlayerPreventsWholeSessionAbandonment()
    {
        var clock = new ManualSessionClock();
        var store = SessionTestData.Store(clock);
        var created = await store.CreateAsync(SessionTestData.Players());
        await store.UpdateAsync(created.Id, session => session.Connect(created.Game.Players[0].Id, Guid.NewGuid()));
        clock.Advance(TimeSpan.FromDays(1));
        Assert.Equal(0, await store.CleanupAsync());
        Assert.Equal(SessionStatus.Waiting, (await store.GetAsync(created.Id))!.Status);
    }

    [Fact]
    public async Task ReconnectionResetsOfflineGraceAndMultipleTabsStayConnected()
    {
        var clock = new ManualSessionClock();
        var store = SessionTestData.Store(clock);
        var created = await store.CreateAsync(SessionTestData.Players());
        var player = created.Game.Players[0].Id;
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        await store.UpdateAsync(created.Id, session =>
        {
            session.Connect(player, first);
            session.Connect(player, second);
            session.Connect(player, second);
        });
        await store.UpdateAsync(created.Id, session => session.Disconnect(player, first));
        clock.Advance(TimeSpan.FromMinutes(3));
        Assert.Equal(0, await store.CleanupAsync());
        Assert.Equal(1, (await store.GetAsync(created.Id))!.Players[0].ConnectionCount);
        await store.UpdateAsync(created.Id, session => session.Disconnect(player, second));
        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(0, await store.CleanupAsync());
        Assert.Equal(SessionStatus.Waiting, (await store.GetAsync(created.Id))!.Status);
    }

    [Fact]
    public async Task CompletedRetentionIsAnchoredToCompletionEvenWhenPlayersReconnect()
    {
        var clock = new ManualSessionClock();
        var store = SessionTestData.Store(clock);
        var created = await store.CreateAsync(SessionTestData.Players());
        var completed = await store.UpdateAsync(created.Id, SessionTestData.Complete);
        Assert.Equal(SessionStatus.Completed, completed.Status);
        clock.Advance(TimeSpan.FromMinutes(9));
        await store.UpdateAsync(created.Id, session => session.Connect(created.Game.Players[0].Id, Guid.NewGuid()));
        Assert.Equal(0, await store.CleanupAsync());
        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(1, await store.CleanupAsync());
        Assert.Null(await store.GetAsync(created.Id));
    }

    [Fact]
    public async Task ConcurrentCleanupSweepsRemoveEachTerminalSessionOnce()
    {
        var clock = new ManualSessionClock();
        var store = SessionTestData.Store(clock, TimeSpan.Zero);
        var created = await store.CreateAsync(SessionTestData.Players());
        await store.UpdateAsync(created.Id, session => session.Abandon());
        var counts = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(() => store.CleanupAsync())));

        Assert.Equal(1, counts.Sum());
        Assert.Null(await store.GetAsync(created.Id));
    }

    [Fact]
    public async Task NotificationsExposeCurrentAndCommittedVersionsButNoFailedUpdates()
    {
        var store = SessionTestData.Store(new ManualSessionClock());
        var created = await store.CreateAsync(SessionTestData.Players());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var observer = store.WatchAsync(created.Id, timeout.Token).GetAsyncEnumerator();
        Assert.True(await observer.MoveNextAsync());
        Assert.Equal(0, observer.Current.Version);
        await store.UpdateAsync(created.Id, session => session.BeginStarting());
        Assert.True(await observer.MoveNextAsync());
        Assert.Equal(1, observer.Current.Version);
        Assert.Equal(SessionStatus.Starting, observer.Current.Status);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.UpdateAsync(created.Id, session => session.BeginStarting()));
        await store.UpdateAsync(created.Id, session => session.Connect(created.Game.Players[0].Id, Guid.NewGuid()));
        Assert.True(await observer.MoveNextAsync());
        Assert.Equal(2, observer.Current.Version);
        Assert.Equal(created.Id, observer.Current.SessionId);
    }

    [Fact]
    public async Task SlowObserversReceiveLatestRevisionAndRemovalClosesTheStream()
    {
        var store = SessionTestData.Store(new ManualSessionClock(), TimeSpan.Zero);
        var created = await store.CreateAsync(SessionTestData.Players());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var observer = store.WatchAsync(created.Id, timeout.Token).GetAsyncEnumerator();
        Assert.True(await observer.MoveNextAsync());
        for (var index = 0; index < 10; index++)
        {
            await store.UpdateAsync(created.Id, session => session.Connect(created.Game.Players[0].Id, Guid.NewGuid()));
        }
        Assert.True(await observer.MoveNextAsync());
        Assert.Equal(10, observer.Current.Version);
        await store.UpdateAsync(created.Id, session => session.Abandon());
        Assert.Equal(1, await store.CleanupAsync());
        Assert.True(await observer.MoveNextAsync());
        Assert.Equal(SessionChangeKind.Removed, observer.Current.Kind);
        Assert.Equal(12, observer.Current.Version);
        Assert.False(await observer.MoveNextAsync());
    }

    [Fact]
    public async Task CancelledObserversCanBeDisposedAndDoNotBlockFurtherUpdates()
    {
        var store = SessionTestData.Store(new ManualSessionClock());
        var created = await store.CreateAsync(SessionTestData.Players());
        using var cancellation = new CancellationTokenSource();
        await using (var observer = store.WatchAsync(created.Id, cancellation.Token).GetAsyncEnumerator())
        {
            Assert.True(await observer.MoveNextAsync());
            var next = observer.MoveNextAsync().AsTask();
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => next);
        }

        var updated = await store.UpdateAsync(created.Id, session => session.BeginStarting());
        Assert.Equal(1, updated.Version);
    }
}
