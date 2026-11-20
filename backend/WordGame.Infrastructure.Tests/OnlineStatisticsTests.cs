using WordGame.Application.Sessions;
using WordGame.Application.Statistics;
using WordGame.Domain;
using WordGame.Infrastructure.Sessions;
using WordGame.Infrastructure.Statistics;

namespace WordGame.Infrastructure.Tests;

public sealed class OnlineStatisticsTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "wordgame-statistics", Guid.NewGuid().ToString("N"));
    private readonly Guid player = Guid.NewGuid();

    [Fact]
    public async Task ReopeningStorePreservesHistoryAndConcurrentDuplicateWritesCountOnce()
    {
        var store = new JsonOnlineStatisticsStore(directory);
        var match = Match(won: true);
        await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => store.RecordAsync(match)));
        await store.RecordAsync(match with { Players = [new(player, "Changed", 999, false, false, 99, 99)] });
        var reopened = new JsonOnlineStatisticsStore(directory);
        var saved = Assert.Single(await reopened.GetHistoryAsync(player));
        Assert.Equal(match.MatchId, saved.MatchId);
        Assert.Equal(20, Assert.Single(saved.Players).Score);
        Assert.True(Assert.Single(saved.Players).Won);
        Assert.Empty(await reopened.GetHistoryAsync(Guid.NewGuid()));
        Assert.Single(Directory.GetFiles(directory));
    }

    [Fact]
    public async Task WinsLossesDrawsAndBluffCallsAreCalculatedAcrossPersistentMatches()
    {
        var store = new JsonOnlineStatisticsStore(directory);
        await store.RecordAsync(Match(won: true));
        await store.RecordAsync(Match(won: false));
        await store.RecordAsync(Match(won: false, draw: true));
        var statistics = OnlinePlayerStatistics.Calculate(player, await new JsonOnlineStatisticsStore(directory).GetHistoryAsync(player));
        Assert.Equal(new OnlinePlayerStatistics(3, 1, 1, 1, 33.33, 6, 3), statistics);
        Assert.Equal(new OnlinePlayerStatistics(0, 0, 0, 0, 0, 0, 0), OnlinePlayerStatistics.Calculate(Guid.NewGuid(), []));
    }

    [Fact]
    public async Task CancelledWriteDoesNotPublishPartialHistory()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new JsonOnlineStatisticsStore(directory).RecordAsync(Match(true), cancellation.Token));
        Assert.Empty(Directory.GetFiles(directory));
    }

    private OnlineMatchHistory Match(bool won, bool draw = false) => new(Guid.NewGuid(), DateTimeOffset.UtcNow,
        MatchEndReason.Completed, [new(player, "Player", 20, won, draw, 2, 1)]);

    [Fact]
    public async Task FailedPersistenceLeavesCompletionUncommittedAndRetryable()
    {
        var statistics = new FailingStore();
        var store = new InMemoryGameSessionStore(new ManualSessionClock(),
            new SessionRetention(TimeSpan.FromMinutes(2), TimeSpan.Zero, TimeSpan.Zero), statistics);
        var players = SessionTestData.Players();
        var created = await store.CreateAsync(players, settings: MatchSettings.Online());
        var active = await store.UpdateAsync(created.Id, SessionTestData.Activate);
        void Complete(GameSession session)
        {
            session.Game.CompleteByForfeit(players[0].Id, MatchEndReason.Forfeit);
            session.Complete();
        }
        await Assert.ThrowsAsync<IOException>(() => store.UpdateAsync(created.Id, active.Version, Complete));
        Assert.Equal(active.Version, (await store.GetAsync(created.Id))!.Version);
        Assert.Equal(SessionStatus.Active, (await store.GetAsync(created.Id))!.Status);
        statistics.Fail = false;
        Assert.Equal(SessionStatus.Completed, (await store.UpdateAsync(created.Id, active.Version, Complete)).Status);
        Assert.NotNull(statistics.Saved);
    }

    [Fact]
    public async Task CleanupTimeoutPersistsResultAndSessionRemovalDoesNotDeleteHistory()
    {
        var clock = new ManualSessionClock();
        var statistics = new JsonOnlineStatisticsStore(directory);
        var store = new InMemoryGameSessionStore(clock,
            new SessionRetention(TimeSpan.FromMinutes(2), TimeSpan.Zero, TimeSpan.Zero), statistics);
        var players = SessionTestData.Players();
        var created = await store.CreateAsync(players, settings: MatchSettings.Online());
        await store.UpdateAsync(created.Id, session =>
        {
            SessionTestData.Activate(session);
            session.Connect(players[1].Id, Guid.NewGuid());
        });
        clock.Advance(TimeSpan.FromSeconds(30));
        Assert.Equal(1, await store.CleanupAsync());
        Assert.Null(await store.GetAsync(created.Id));
        var match = Assert.Single(await new JsonOnlineStatisticsStore(directory).GetHistoryAsync(players[1].Id));
        Assert.Equal(MatchEndReason.ReconnectionExpired, match.Reason);
        Assert.True(match.Players.Single(player => player.PlayerId == players[1].Id).Won);
    }

    private sealed class FailingStore : IOnlineStatisticsStore
    {
        internal bool Fail = true;
        internal OnlineMatchHistory? Saved;
        public Task RecordAsync(OnlineMatchHistory match, CancellationToken cancellationToken = default)
        {
            if (Fail) { throw new IOException("Simulated disk failure"); }
            Saved = match; return Task.CompletedTask;
        }
        public Task<IReadOnlyList<OnlineMatchHistory>> GetHistoryAsync(Guid playerId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<OnlineMatchHistory>>([]);
    }

    public void Dispose() { if (Directory.Exists(directory)) { Directory.Delete(directory, recursive: true); } }
}
