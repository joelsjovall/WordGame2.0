namespace WordGame.Domain;

public sealed record PlayerTurn
{
    public PlayerTurn(Guid playerId, int number)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(number);
        PlayerId = DomainGuard.Id(playerId, nameof(playerId));
        Number = number;
    }

    public Guid PlayerId { get; }
    public int Number { get; }
}
