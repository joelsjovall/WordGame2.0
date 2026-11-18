using WordGame.Application.Sessions;
using WordGame.Domain;

namespace WordGame.Application.Tests.Games;

// Minimal single-threaded unit-test double. Real store serialization is tested separately in Infrastructure.Tests.
internal sealed class EngineTestStore(TimeProvider clock) : IGameSessionStore
{
    private readonly Dictionary<Guid, (GameSession Session, long Version)> entries = [];

    public Task<SessionSnapshot> CreateAsync(IEnumerable<Player> players, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var session = new GameSession(Guid.NewGuid(), players, clock.GetUtcNow());
        entries.Add(session.Id, (session, 0));
        return Task.FromResult(session.Snapshot(0));
    }

    public Task<SessionSnapshot?> GetAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(entries.TryGetValue(sessionId, out var entry) ? entry.Session.Snapshot(entry.Version) : null);
    }

    public Task<SessionSnapshot> UpdateAsync(Guid sessionId, Action<GameSession> update, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entry = entries[sessionId];
        var draft = entry.Session.Copy(clock.GetUtcNow());
        update(draft);
        draft.EnsureConsistent();
        cancellationToken.ThrowIfCancellationRequested();
        var next = (Session: draft.Copy(draft.UpdatedAt), Version: entry.Version + 1);
        entries[sessionId] = next;
        return Task.FromResult(next.Session.Snapshot(next.Version));
    }

    public Task<SessionSnapshot> UpdateAsync(Guid sessionId, long expectedVersion, Action<GameSession> update, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (entries[sessionId].Version != expectedVersion)
        {
            throw new SessionVersionConflictException();
        }

        return UpdateAsync(sessionId, update, cancellationToken);
    }

    public IAsyncEnumerable<SessionChange> WatchAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Engine unit tests do not use notifications.");

    public Task<int> CleanupAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Engine unit tests do not run cleanup.");
}

internal sealed class EngineTestClock : TimeProvider
{
    private DateTimeOffset now = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => now;
    public void Advance(TimeSpan duration) => now += duration;
}
