namespace WordGame.Domain;

public sealed record LobbyCode
{
    public const int Length = 6;
    public const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public LobbyCode(string value)
    {
        var normalized = DomainGuard.Text(value, nameof(value)).ToUpperInvariant();
        if (normalized.Length != Length || normalized.Any(character => !Alphabet.Contains(character)))
        {
            throw new ArgumentException("A lobby code requires six unambiguous letters or digits.", nameof(value));
        }
        Value = normalized;
    }

    public string Value { get; }
}
