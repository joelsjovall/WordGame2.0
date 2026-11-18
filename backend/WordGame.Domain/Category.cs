namespace WordGame.Domain;

public sealed record Category
{
    public Category(string id, string name, CategoryDifficulty difficulty)
    {
        if (!Enum.IsDefined(difficulty))
        {
            throw new ArgumentOutOfRangeException(nameof(difficulty));
        }

        Id = DomainGuard.Text(id, nameof(id));
        Name = DomainGuard.Text(name, nameof(name));
        Difficulty = difficulty;
    }

    public string Id { get; }
    public string Name { get; }
    public CategoryDifficulty Difficulty { get; }
}
