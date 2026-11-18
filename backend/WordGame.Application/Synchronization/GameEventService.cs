using System.Runtime.CompilerServices;
using WordGame.Application.Sessions;

namespace WordGame.Application.Synchronization;

// Notifications are invalidations, never a second gameplay state store.
public sealed class GameEventService(IGameSessionStore sessions)
{
    public async Task<SessionSnapshot?> GetStateAsync(Guid gameId, Guid playerId, CancellationToken cancellationToken = default)
    {
        var snapshot = await sessions.GetAsync(gameId, cancellationToken);
        return snapshot?.Game.Players.Any(player => player.Id == playerId) == true ? snapshot : null;
    }

    public async IAsyncEnumerable<SessionChange> WatchAsync(Guid gameId, Guid playerId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var snapshot = await GetStateAsync(gameId, playerId, cancellationToken);
        if (snapshot is null)
        {
            yield break;
        }
        await using var changes = sessions.WatchAsync(gameId, cancellationToken).GetAsyncEnumerator(cancellationToken);
        while (true)
        {
            bool available;
            var removed = false;
            try
            {
                available = await changes.MoveNextAsync();
            }
            catch (KeyNotFoundException)
            {
                available = false;
                removed = true;
            }
            if (removed)
            {
                yield return new SessionChange(gameId, snapshot.Version, snapshot.Status, SessionChangeKind.Removed);
            }
            if (!available)
            {
                yield break;
            }

            var change = changes.Current;
            if (change.Kind == SessionChangeKind.Removed || await GetStateAsync(gameId, playerId, cancellationToken) is not null)
            {
                yield return change;
            }
        }
    }
}
