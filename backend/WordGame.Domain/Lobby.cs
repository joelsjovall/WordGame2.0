namespace WordGame.Domain;

public enum LobbyStatus { Waiting }

// A pregame lobby can have one host; the gameplay engine still requires a fixed roster of 2–4.
public sealed class Lobby
{
    public const int MinimumPlayers = Game.MinimumPlayers;
    public const int MaximumPlayers = Game.MaximumPlayers;
    public const int MaximumDisplayNameLength = 32;

    public Lobby(Guid id, LobbyCode code, Player host, DateTimeOffset createdAt, DateTimeOffset expiresAt)
    {
        Id = DomainGuard.Id(id, nameof(id));
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(host);
        ValidateName(host);
        if (expiresAt <= createdAt)
        {
            throw new ArgumentOutOfRangeException(nameof(expiresAt));
        }
        Code = code;
        HostPlayerId = host.Id;
        Players = Array.AsReadOnly(new[] { host });
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; }
    public LobbyCode Code { get; }
    public Guid HostPlayerId { get; }
    public LobbyStatus Status => LobbyStatus.Waiting;
    public IReadOnlyList<Player> Players { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset ExpiresAt { get; }

    private Lobby(Lobby original, Player player)
    {
        Id = original.Id;
        Code = original.Code;
        HostPlayerId = original.HostPlayerId;
        CreatedAt = original.CreatedAt;
        ExpiresAt = original.ExpiresAt;
        Players = Array.AsReadOnly(original.Players.Append(player).ToArray());
    }

    public Lobby Join(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        ValidateName(player);
        var existing = Players.SingleOrDefault(member => member.Id == player.Id);
        if (existing is not null)
        {
            if (!StringComparer.OrdinalIgnoreCase.Equals(existing.DisplayName, player.DisplayName))
            {
                throw new LobbyJoinException(LobbyJoinFailure.DifferentPlayerName);
            }

            return this;
        }
        if (Players.Count >= MaximumPlayers)
        {
            throw new LobbyJoinException(LobbyJoinFailure.Full);
        }

        if (Players.Any(member => StringComparer.OrdinalIgnoreCase.Equals(member.DisplayName, player.DisplayName)))
        {
            throw new LobbyJoinException(LobbyJoinFailure.DisplayNameTaken);
        }

        return new Lobby(this, player);
    }

    private static void ValidateName(Player player)
    {
        if (player.DisplayName.Length > MaximumDisplayNameLength || player.DisplayName.Any(char.IsControl))
        {
            throw new ArgumentException("Display names must contain at most 32 characters and no control characters.", nameof(player));
        }
    }
}
