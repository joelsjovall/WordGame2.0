namespace WordGame.Domain;

public enum MatchFormat { Party, Online }

public sealed class MatchSettings
{
    private MatchSettings(MatchFormat format, int roundCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(roundCount);
        Format = format;
        RoundCount = roundCount;
    }

    public MatchFormat Format { get; }
    public int RoundCount { get; }

    public static MatchSettings Party(int roundCount) => new(MatchFormat.Party, roundCount);
    public static MatchSettings Online() => new(MatchFormat.Online, 6);
}
