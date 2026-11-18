namespace WordGame.Application.Challenges;

public sealed class ChallengeRules
{
    public ChallengeRules(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "Challenge duration must be positive.");
        }

        Duration = duration;
    }

    public TimeSpan Duration { get; }
}
