namespace WordGame.Application.Statistics;

// A match ID is the durable idempotency key; implementations must never replace a recorded result.
public interface IOnlineStatisticsStore
{
    Task RecordAsync(OnlineMatchHistory match, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OnlineMatchHistory>> GetHistoryAsync(Guid playerId, CancellationToken cancellationToken = default);
}
