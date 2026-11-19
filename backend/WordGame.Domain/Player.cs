namespace WordGame.Domain;

public sealed record Player
{
    public const int MaximumDisplayNameLength = 32;
    public Player(Guid id, string displayName)
    {
        Id = DomainGuard.Id(id, nameof(id));
        DisplayName = DomainGuard.Text(displayName, nameof(displayName));
    }

    public Guid Id { get; }
    public string DisplayName { get; }

    public void ValidateDisplayName()
    {
        if (DisplayName.Length > MaximumDisplayNameLength || DisplayName.Any(char.IsControl))
        {
            throw new ArgumentException("Display names must contain at most 32 characters and no control characters.", "displayName");
        }
    }
}
