using WordGame.Domain;

namespace WordGame.Application.Sessions;

public interface IGameSessionStore
{
    Task<SessionSnapshot> CreateAsync(IEnumerable<Player> players, CancellationToken cancellationToken = default, MatchSettings? settings = null);
    Task<SessionSnapshot?> GetAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SessionSnapshot>> GetActiveAsync(CancellationToken cancellationToken = default);

    // Synchronous trusted backend work. Successful actions commit atomically; failures roll back.
    Task<SessionSnapshot> UpdateAsync(
        Guid sessionId, Action<GameSession> update, CancellationToken cancellationToken = default);

    // Compare and execute under the same session lock. A conflict must not invoke the callback.
    Task<SessionSnapshot> UpdateAsync(
        Guid sessionId, long expectedVersion, Action<GameSession> update, CancellationToken cancellationToken = default);

    // Emits the current revision on subscription; slow consumers may receive only the latest revision.
    IAsyncEnumerable<SessionChange> WatchAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<int> CleanupAsync(CancellationToken cancellationToken = default);
}
