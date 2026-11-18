namespace WordGame.Domain;

public sealed class AcceptedAnswer
{
    public AcceptedAnswer(string value, IEnumerable<string>? aliases = null, string? id = null)
    {
        Value = DomainGuard.Text(value, nameof(value));
        Id = id is null ? WordNormalizer.Normalize(Value) : DomainGuard.Text(id, nameof(id));
        Aliases = Array.AsReadOnly((aliases ?? [])
            .Select(alias => DomainGuard.Text(alias, nameof(aliases)))
            .Distinct(StringComparer.Ordinal)
            .ToArray());
    }

    public string Id { get; }
    public string Value { get; }
    public IReadOnlyList<string> Aliases { get; }
}
