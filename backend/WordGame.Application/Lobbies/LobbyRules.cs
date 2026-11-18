namespace WordGame.Application.Lobbies;

public sealed class LobbyRules
{
    public LobbyRules(TimeSpan lifetime)
    {
        if (lifetime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(lifetime));
        }

        Lifetime = lifetime;
    }

    public TimeSpan Lifetime { get; }
}
