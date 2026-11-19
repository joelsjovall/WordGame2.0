namespace WordGame.Application.Lobbies;

public sealed class LobbyRules
{
    public LobbyRules(TimeSpan lifetime, TimeSpan? disconnectedGrace = null)
    {
        if (lifetime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(lifetime));
        }

        Lifetime = lifetime;
        DisconnectedGrace = disconnectedGrace ?? TimeSpan.FromMinutes(2);
        if (DisconnectedGrace <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(disconnectedGrace));
        }
    }

    public TimeSpan Lifetime { get; }
    public TimeSpan DisconnectedGrace { get; }
}
