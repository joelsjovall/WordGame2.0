namespace WordGame.Domain;

public sealed class LobbySettings
{
    public LobbySettings(int maximumPlayers = 4, int? roundCount = null, int challengeSeconds = 30,
        IEnumerable<CategoryDifficulty>? allowedDifficulties = null, IEnumerable<string>? allowedCategoryIds = null)
    {
        if (maximumPlayers is < Game.MinimumPlayers or > Game.MaximumPlayers || roundCount is < 1 or > 100 ||
            challengeSeconds is < 5 or > 300)
        {
            throw new ArgumentException("Use 2–4 players, 1–100 rounds (or automatic), and a 5–300 second challenge.");
        }
        var difficulties = (allowedDifficulties ?? Enum.GetValues<CategoryDifficulty>()).ToArray();
        if (difficulties.Length == 0 || difficulties.Any(value => !Enum.IsDefined(value)) || difficulties.Distinct().Count() != difficulties.Length)
        {
            throw new ArgumentException("Select at least one distinct valid difficulty.");
        }
        var categoryIds = allowedCategoryIds?.ToArray();
        if (categoryIds is not null && (categoryIds.Length is < 1 or > 100 ||
            categoryIds.Any(id => string.IsNullOrWhiteSpace(id) || id.Length > 128) ||
            categoryIds.Distinct(StringComparer.Ordinal).Count() != categoryIds.Length))
        {
            throw new ArgumentException("Choose one to 100 distinct category IDs.", nameof(allowedCategoryIds));
        }
        MaximumPlayers = maximumPlayers;
        RoundCount = roundCount;
        ChallengeSeconds = challengeSeconds;
        AllowedDifficulties = Array.AsReadOnly(difficulties.Order().ToArray());
        AllowedCategoryIds = categoryIds is null ? null : Array.AsReadOnly(categoryIds);
    }

    public int MaximumPlayers { get; }
    public int? RoundCount { get; }
    public int ChallengeSeconds { get; }
    public IReadOnlyList<CategoryDifficulty> AllowedDifficulties { get; }
    public IReadOnlyList<string>? AllowedCategoryIds { get; }
    public MatchSettings ForMatch(int playerCount) => MatchSettings.Party(RoundCount ?? playerCount * 2,
        TimeSpan.FromSeconds(ChallengeSeconds), AllowedDifficulties, AllowedCategoryIds);
}

public sealed class LobbyActionException(string message, bool forbidden = false) : InvalidOperationException(message)
{
    public bool Forbidden { get; } = forbidden;
}
