using WordGame.Domain;

namespace WordGame.Application.Matchmaking;

public enum MatchmakingStatus { Idle, Searching, Matched, Ready, Failed, Cancelled, Expired }

public sealed record MatchmakingSearch(Guid Id, Player Player, Guid CredentialSessionId, MatchmakingStatus Status,
    DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, DateTimeOffset UpdatedAt, Guid? MatchId = null,
    Guid? GameId = null, bool HasAccepted = false);

public sealed class MatchmakingQueueState
{
    public Dictionary<Guid, MatchmakingSearch> Searches { get; } = [];
    public LinkedList<Guid> Waiting { get; } = [];
    public Dictionary<Guid, Guid> LatestByPlayer { get; } = [];
}

// Transitions run serially; only detached immutable search records may escape the callback.
public interface IMatchmakingQueueStore
{
    Task<T> ExecuteAsync<T>(Func<MatchmakingQueueState, Task<T>> transition, CancellationToken cancellationToken = default);
    IAsyncEnumerable<MatchmakingSearch?> WatchAsync(Guid playerId, CancellationToken cancellationToken = default);
}

public interface IMatchmakingPolicy
{
    IReadOnlyList<MatchmakingSearch> SelectGroup(IReadOnlyList<MatchmakingSearch> waiting);
}

public sealed class FifoDuelPolicy : IMatchmakingPolicy
{
    public IReadOnlyList<MatchmakingSearch> SelectGroup(IReadOnlyList<MatchmakingSearch> waiting) =>
        waiting.Count >= 2 ? waiting.Take(2).ToArray() : [];
}

public sealed class MatchmakingConflictException(string message) : Exception(message);

public sealed class MatchmakingRules
{
    public MatchmakingRules(TimeSpan? searchLifetime = null, TimeSpan? reservationLifetime = null, TimeSpan? terminalRetention = null,
        TimeSpan? acceptanceTimeout = null)
    {
        SearchLifetime = searchLifetime ?? TimeSpan.FromMinutes(2);
        ReservationLifetime = reservationLifetime ?? TimeSpan.FromMinutes(1);
        TerminalRetention = terminalRetention ?? TimeSpan.FromMinutes(10);
        AcceptanceTimeout = acceptanceTimeout ?? reservationLifetime ?? TimeSpan.FromSeconds(30);
        if (SearchLifetime <= TimeSpan.Zero || ReservationLifetime <= TimeSpan.Zero || TerminalRetention <= TimeSpan.Zero || AcceptanceTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(searchLifetime), "Matchmaking lifetimes must be positive.");
        }
    }
    public TimeSpan SearchLifetime { get; }
    public TimeSpan ReservationLifetime { get; }
    public TimeSpan TerminalRetention { get; }
    public TimeSpan AcceptanceTimeout { get; }
}
