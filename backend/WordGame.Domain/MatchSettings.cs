namespace WordGame.Domain;

public enum MatchFormat { Party, Online }

public sealed class MatchSettings
{
    private MatchSettings(MatchFormat format, int roundCount, TimeSpan? challengeDuration = null,
        IEnumerable<CategoryDifficulty>? allowedDifficulties = null, IEnumerable<string>? allowedCategoryIds = null)
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

        var categoryIds = allowedCategoryIds?.ToArray();
        if (categoryIds is not null && (categoryIds.Length is < 1 or > 100 ||
            categoryIds.Any(id => string.IsNullOrWhiteSpace(id) || id.Length > 128) ||
            categoryIds.Distinct(StringComparer.Ordinal).Count() != categoryIds.Length))
        {
            throw new ArgumentException("Choose one to 100 distinct category IDs.", nameof(allowedCategoryIds));
        }
        ChallengeDuration = challengeDuration;
        AllowedDifficulties = allowed is null ? null : Array.AsReadOnly(allowed.Distinct().Order().ToArray());
        AllowedCategoryIds = categoryIds is null ? null : Array.AsReadOnly(categoryIds);
    }

    public MatchFormat Format { get; }
    public int RoundCount { get; }
    public TimeSpan? ChallengeDuration { get; }
    public IReadOnlyList<CategoryDifficulty>? AllowedDifficulties { get; }
    public IReadOnlyList<string>? AllowedCategoryIds { get; }

    public static MatchSettings Party(int roundCount, TimeSpan? challengeDuration = null,
        IEnumerable<CategoryDifficulty>? allowedDifficulties = null, IEnumerable<string>? allowedCategoryIds = null) =>
        new(MatchFormat.Party, roundCount, challengeDuration, allowedDifficulties, allowedCategoryIds);
    public static MatchSettings Online() => new(MatchFormat.Online, 6);
}
