using System.Collections.Frozen;
using WordGame.Domain;

namespace WordGame.Application.Categories;

public enum CategoryTimeoutBehavior
{
    AutoSelect,
    CancelMatch
}

public sealed class CategorySelectionRules
{
    public CategorySelectionRules(TimeSpan? timeout, IEnumerable<CategoryDifficulty>? allowedDifficulties = null,
        IReadOnlyDictionary<CategoryDifficulty, int>? scoreMultipliers = null,
        CategoryTimeoutBehavior timeoutBehavior = CategoryTimeoutBehavior.AutoSelect)
    {
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        if (!Enum.IsDefined(timeoutBehavior))
        {
            throw new ArgumentOutOfRangeException(nameof(timeoutBehavior));
        }

        var allowed = (allowedDifficulties ?? Enum.GetValues<CategoryDifficulty>()).ToArray();
        if (allowed.Length == 0 || allowed.Any(difficulty => !Enum.IsDefined(difficulty)))
        {
            throw new ArgumentException("At least one valid difficulty must be enabled.", nameof(allowedDifficulties));
        }

        var multipliers = scoreMultipliers ?? Enum.GetValues<CategoryDifficulty>().ToDictionary(difficulty => difficulty, ScoringRules.DefaultPointsPerWord);
        if (multipliers.Count != Enum.GetValues<CategoryDifficulty>().Length ||
            Enum.GetValues<CategoryDifficulty>().Any(difficulty => !multipliers.TryGetValue(difficulty, out var value) || value <= 0))
        {
            throw new ArgumentException("Every difficulty requires a positive integer multiplier.", nameof(scoreMultipliers));
        }

        Timeout = timeout;
        TimeoutBehavior = timeoutBehavior;
        AllowedDifficulties = allowed.ToFrozenSet();
        ScoreMultipliers = multipliers.ToFrozenDictionary();
    }

    public TimeSpan? Timeout { get; }
    public CategoryTimeoutBehavior TimeoutBehavior { get; }
    public IReadOnlySet<CategoryDifficulty> AllowedDifficulties { get; }
    public IReadOnlyDictionary<CategoryDifficulty, int> ScoreMultipliers { get; }
}
