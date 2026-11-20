using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using WordGame.Application.Sessions;
using WordGame.Application.Statistics;
using WordGame.Domain;

namespace WordGame.Infrastructure.Sessions;

public sealed class InMemoryGameSessionStore(TimeProvider clock, SessionRetention retention, IOnlineStatisticsStore? statistics = null) : IGameSessionStore
{
    private readonly ConcurrentDictionary<Guid, SessionEntry> sessions = new();

    public Task<SessionSnapshot> CreateAsync(IEnumerable<Player> players, CancellationToken cancellationToken = default, MatchSettings? settings = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var session = new GameSession(Guid.NewGuid(), players, clock.GetUtcNow(), settings);
        if (!sessions.TryAdd(session.Id, new SessionEntry(session)))
        {
            throw new InvalidOperationException("A generated session ID collided with an existing session.");
        }

        return Task.FromResult(session.Snapshot(0));
    }

    public async Task<SessionSnapshot?> GetAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!sessions.TryGetValue(sessionId, out var entry))
        {
            return null;
        }

        await entry.Gate.WaitAsync(cancellationToken);
        try
        {
            return entry.Removed ? null : entry.Session.Snapshot(entry.Version);
        }
        finally
        {
            entry.Gate.Release();
        }
    }

    public Task<SessionSnapshot> UpdateAsync(Guid sessionId, Action<GameSession> update, CancellationToken cancellationToken = default) =>
        UpdateCoreAsync(sessionId, update, null, cancellationToken);

    public async Task<IReadOnlyList<SessionSnapshot>> GetOpenAsync(CancellationToken cancellationToken = default)
    {
        var open = new List<SessionSnapshot>();
        foreach (var id in sessions.Keys.ToArray())
        {
            var snapshot = await GetAsync(id, cancellationToken);
            if (snapshot?.Status is SessionStatus.Waiting or SessionStatus.Starting or SessionStatus.Active)
            {
                open.Add(snapshot);
            }
        }
        return open.AsReadOnly();
    }

    public async Task<IReadOnlyList<SessionSnapshot>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        var active = new List<SessionSnapshot>();
        foreach (var id in sessions.Keys.ToArray())
        {
            var snapshot = await GetAsync(id, cancellationToken);
            if (snapshot?.Status == SessionStatus.Active)
            {
                active.Add(snapshot);
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        return active.AsReadOnly();
    }

    public Task<SessionSnapshot> UpdateAsync(Guid sessionId, long expectedVersion, Action<GameSession> update, CancellationToken cancellationToken = default) =>
        UpdateCoreAsync(sessionId, update, expectedVersion, cancellationToken);

    private async Task<SessionSnapshot> UpdateCoreAsync(
        Guid sessionId, Action<GameSession> update, long? expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(update);
        cancellationToken.ThrowIfCancellationRequested();
        var entry = Find(sessionId);
        await entry.Gate.WaitAsync(cancellationToken);
        try
        {
            EnsurePresent(entry);
            if (expectedVersion is { } version && version != entry.Version)
            {
                throw new SessionVersionConflictException();
            }

            var draft = entry.Session.Copy(clock.GetUtcNow());
            update(draft);
            draft.EnsureConsistent();
            cancellationToken.ThrowIfCancellationRequested();
            // A callback may retain its draft; publish a separate deep copy.
            var committed = draft.Copy(draft.UpdatedAt);
            var nextVersion = checked(entry.Version + 1);
            var result = committed.Snapshot(nextVersion);
            // Persist before publishing completion. Failure leaves the active draft uncommitted and retryable.
            if (statistics is not null && OnlineMatchHistory.From(result) is { } completed)
            {
                await statistics.RecordAsync(completed, cancellationToken);
            }
            entry.Session = committed;
            entry.Version = nextVersion;
            entry.Publish(SessionChangeKind.Updated);
            return result;
        }
        finally
        {
            entry.Gate.Release();
        }
    }

    public async IAsyncEnumerable<SessionChange> WatchAsync(
        Guid sessionId, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entry = Find(sessionId);
        var subscriberId = Guid.NewGuid();
        var channel = Channel.CreateBounded<SessionChange>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            AllowSynchronousContinuations = false,
            SingleReader = true
        });
        await entry.Gate.WaitAsync(cancellationToken);
        try
        {
            EnsurePresent(entry);
            entry.Subscribers.Add(subscriberId, channel);
            channel.Writer.TryWrite(new SessionChange(sessionId, entry.Version, entry.Session.Status, SessionChangeKind.Updated));
        }
        finally
        {
            entry.Gate.Release();
        }

        try
        {
            await foreach (var change in channel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return change;
            }
        }
        finally
        {
            await entry.Gate.WaitAsync(CancellationToken.None);
            try
            {
                entry.Subscribers.Remove(subscriberId);
                channel.Writer.TryComplete();
            }
            finally
            {
                entry.Gate.Release();
            }
        }
    }

    public async Task<int> CleanupAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var removed = 0;
        foreach (var (id, entry) in sessions.ToArray())
        {
            await entry.Gate.WaitAsync(cancellationToken);
            try
            {
                if (entry.Removed)
                {
                    continue;
                }

                var now = clock.GetUtcNow();
                if (entry.Session.Status is not (SessionStatus.Completed or SessionStatus.Abandoned))
                {
                    var draft = entry.Session.Copy(now);
                    bool changed;
                    if (draft.Game.Settings.Format == MatchFormat.Online && draft.Status == SessionStatus.Starting)
                    {
                        // Matchmaking owns the acceptance deadline and cancels unaccepted games.
                        changed = false;
                    }
                    else if (draft.AllDisconnectedSince is { } disconnected && now - disconnected >= retention.GraceFor(draft.Game.Settings.Format))
                    {
                        draft.Abandon();
                        changed = true;
                    }
                    else
                    {
                        changed = draft.ReconcileTurns(retention.GraceFor(draft.Game.Settings.Format));
                    }

                    if (changed)
                    {
                        draft.EnsureConsistent();
                        var nextVersion = checked(entry.Version + 1);
                        if (statistics is not null && OnlineMatchHistory.From(draft.Snapshot(nextVersion)) is { } completed)
                        {
                            await statistics.RecordAsync(completed, cancellationToken);
                        }
                        entry.Session = draft;
                        entry.Version = nextVersion;
                        entry.Publish(SessionChangeKind.Updated);
                    }
                }

                var keepFor = entry.Session.Status == SessionStatus.Completed
                    ? retention.CompletedRetention : retention.AbandonedRetention;
                if (entry.Session.EndedAt is { } ended && now - ended >= keepFor)
                {
                    var nextVersion = checked(entry.Version + 1);
                    if (sessions.TryRemove(id, out _))
                    {
                        entry.Removed = true;
                        entry.Version = nextVersion;
                        entry.Publish(SessionChangeKind.Removed);
                        removed++;
                    }
                }
            }
            finally
            {
                entry.Gate.Release();
            }
        }

        return removed;
    }

    private SessionEntry Find(Guid id) => sessions.TryGetValue(id, out var entry)
        ? entry : throw new KeyNotFoundException("Game session was not found.");

    private static void EnsurePresent(SessionEntry entry)
    {
        if (entry.Removed)
        {
            throw new KeyNotFoundException("Game session was removed.");
        }
    }
}
