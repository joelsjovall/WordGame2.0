namespace WordGame.Domain;

public sealed record Player
{
    public Player(Guid id, string displayName)
    {
        Id = DomainGuard.Id(id, nameof(id));
        DisplayName = DomainGuard.Text(displayName, nameof(displayName));
    }

    public Guid Id { get; }
    public string DisplayName { get; }
}
