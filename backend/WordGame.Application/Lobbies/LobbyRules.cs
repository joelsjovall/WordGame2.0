namespace WordGame.Application.Lobbies;

public sealed class LobbyRules
{
    public LobbyRules(TimeSpan lifetime, TimeSpan? disconnectedGrace = null, TimeSpan? startCountdown = null)
    {
        if (lifetime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(lifetime));
        }

        Lifetime = lifetime;
        DisconnectedGrace = disconnectedGrace ?? TimeSpan.FromMinutes(2);
        StartCountdown = startCountdown ?? TimeSpan.FromSeconds(3);
        if (StartCountdown < TimeSpan.Zero || StartCountdown > TimeSpan.FromSeconds(10))
        {
            throw new ArgumentOutOfRangeException(nameof(startCountdown));
        }

        if (DisconnectedGrace <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(disconnectedGrace));
        }
    }

    public TimeSpan Lifetime { get; }
    public TimeSpan DisconnectedGrace { get; }
    public TimeSpan StartCountdown { get; }
}
