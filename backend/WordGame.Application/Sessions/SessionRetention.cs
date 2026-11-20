using WordGame.Domain;

namespace WordGame.Application.Sessions;

public sealed class SessionRetention
{
    public SessionRetention(TimeSpan disconnectedGrace, TimeSpan completedRetention, TimeSpan abandonedRetention, TimeSpan? onlineDisconnectedGrace = null)
    {
        if (disconnectedGrace < TimeSpan.Zero || completedRetention < TimeSpan.Zero || abandonedRetention < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(disconnectedGrace), "Retention durations must not be negative.");
        }

        DisconnectedGrace = disconnectedGrace;
        CompletedRetention = completedRetention;
        AbandonedRetention = abandonedRetention;
        OnlineDisconnectedGrace = onlineDisconnectedGrace ?? TimeSpan.FromSeconds(30);
        if (OnlineDisconnectedGrace <= TimeSpan.Zero) { throw new ArgumentOutOfRangeException(nameof(onlineDisconnectedGrace)); }
    }

    public TimeSpan DisconnectedGrace { get; }
    public TimeSpan CompletedRetention { get; }
    public TimeSpan AbandonedRetention { get; }
    public TimeSpan OnlineDisconnectedGrace { get; }
    public TimeSpan GraceFor(MatchFormat format) =>
        format == MatchFormat.Online ? OnlineDisconnectedGrace : DisconnectedGrace;
}
