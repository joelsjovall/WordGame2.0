namespace WordGame.Domain;

public sealed record Bid
{
    public Bid(Guid playerId, int wordCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(wordCount);
        PlayerId = DomainGuard.Id(playerId, nameof(playerId));
        WordCount = wordCount;
    }

    public Guid PlayerId { get; }
    public int WordCount { get; }
}
