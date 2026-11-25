namespace WordGame.Domain;

public sealed record Bid
{
    public Bid(Guid playerId, int wordCount, bool risk = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(wordCount);
        PlayerId = DomainGuard.Id(playerId, nameof(playerId));
        WordCount = wordCount;
        Risk = risk;
    }

    public Guid PlayerId { get; }
    public int WordCount { get; }
    public bool Risk { get; }
}
