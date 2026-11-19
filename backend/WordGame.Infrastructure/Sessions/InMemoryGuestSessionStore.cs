using System.Collections.Concurrent;
using WordGame.Application.Sessions;

namespace WordGame.Infrastructure.Sessions;

public sealed class InMemoryGuestSessionStore(TimeProvider clock) : IGuestSessionStore, IDisposable
{
    private readonly ConcurrentDictionary<Guid, Entry> sessions = new();

    public GuestSession Issue(Guid playerId)
    {
        if (playerId == Guid.Empty)
        {
            throw new ArgumentException("A guest requires a nonempty player ID.", nameof(playerId));
        }
        var now = clock.GetUtcNow();
        foreach (var pair in sessions)
        {
            if (now >= pair.Value.Session.ExpiresAt)
            {
                Revoke(pair.Key);
            }
        }
        while (true)
        {
            var cancellation = new CancellationTokenSource();
            var session = new GuestSession(Guid.NewGuid(), playerId, now, now.AddDays(1), cancellation.Token);
            if (sessions.TryAdd(session.Id, new Entry(session, cancellation)))
            {
                return session;
            }
            cancellation.Dispose();
        }
    }

    public GuestSession? Get(Guid sessionId)
    {
        if (!sessions.TryGetValue(sessionId, out var entry))
        {
            return null;
        }
        if (clock.GetUtcNow() >= entry.Session.ExpiresAt)
        {
            Revoke(sessionId);
            return null;
        }
        return entry.Session;
    }

    public void Revoke(Guid sessionId)
    {
        if (sessions.TryRemove(sessionId, out var entry))
        {
            entry.Cancellation.Cancel();
            entry.Cancellation.Dispose();
        }
    }

    public void Dispose()
    {
        foreach (var id in sessions.Keys)
        {
            Revoke(id);
        }
    }

    private sealed record Entry(GuestSession Session, CancellationTokenSource Cancellation);
}
