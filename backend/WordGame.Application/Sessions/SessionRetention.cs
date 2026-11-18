namespace WordGame.Application.Sessions;

public sealed class SessionRetention
{
    public SessionRetention(TimeSpan disconnectedGrace, TimeSpan completedRetention, TimeSpan abandonedRetention)
    {
        if (disconnectedGrace < TimeSpan.Zero || completedRetention < TimeSpan.Zero || abandonedRetention < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(disconnectedGrace), "Retention durations must not be negative.");
        }

        DisconnectedGrace = disconnectedGrace;
        CompletedRetention = completedRetention;
        AbandonedRetention = abandonedRetention;
    }

    public TimeSpan DisconnectedGrace { get; }
    public TimeSpan CompletedRetention { get; }
    public TimeSpan AbandonedRetention { get; }
}
