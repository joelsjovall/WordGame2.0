namespace WordGame.Domain;

// Represents a nonnegative score total or a server-calculated round award.
public sealed record PlayerScore
{
    public PlayerScore(Guid playerId, int points)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(points);
        PlayerId = DomainGuard.Id(playerId, nameof(playerId));
        Points = points;
    }

    public Guid PlayerId { get; }
    public int Points { get; }
}
