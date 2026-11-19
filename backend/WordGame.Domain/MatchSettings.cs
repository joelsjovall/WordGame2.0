namespace WordGame.Domain;

public enum MatchFormat { Party, Online }

public sealed class MatchSettings
{
    private MatchSettings(MatchFormat format, int roundCount, TimeSpan? challengeDuration = null,
        IEnumerable<CategoryDifficulty>? allowedDifficulties = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(roundCount);
        Format = format;
        RoundCount = roundCount;
        if (challengeDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(challengeDuration));
        }

        var allowed = allowedDifficulties?.ToArray();
        if (allowed is not null && (allowed.Length == 0 || allowed.Any(value => !Enum.IsDefined(value))))
        {
            throw new ArgumentException("Select valid difficulties.", nameof(allowedDifficulties));
        }

        ChallengeDuration = challengeDuration;
        AllowedDifficulties = allowed is null ? null : Array.AsReadOnly(allowed.Distinct().Order().ToArray());
    }

    public MatchFormat Format { get; }
    public int RoundCount { get; }
    public TimeSpan? ChallengeDuration { get; }
    public IReadOnlyList<CategoryDifficulty>? AllowedDifficulties { get; }

    public static MatchSettings Party(int roundCount, TimeSpan? challengeDuration = null,
        IEnumerable<CategoryDifficulty>? allowedDifficulties = null) => new(MatchFormat.Party, roundCount, challengeDuration, allowedDifficulties);
    public static MatchSettings Online() => new(MatchFormat.Online, 6);
}
