namespace WordGame.Domain;

public enum LobbyStatus { Waiting, Starting, Started }

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
    public LobbyStatus Status { get; private init; } = LobbyStatus.Waiting;
    public LobbySettings Settings { get; private init; } = new();
    public long Version { get; private init; }
    public Guid? GameId { get; private init; }
    public IReadOnlyList<Player> Players { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset ExpiresAt { get; }

    private Lobby(Lobby original, Player? player = null)
    {
        Id = original.Id;
        Code = original.Code;
        HostPlayerId = original.HostPlayerId;
        CreatedAt = original.CreatedAt;
        ExpiresAt = original.ExpiresAt;
        Players = player is null ? original.Players : Array.AsReadOnly(original.Players.Append(player).ToArray());
        Status = original.Status;
        Settings = original.Settings;
        GameId = original.GameId;
        Version = checked(original.Version + 1);
    }

    public void RequireHost(Guid actor)
    {
        if (actor != HostPlayerId)
        {
            throw new LobbyActionException("Only the lobby host can perform this action.", forbidden: true);
        }
    }

    public Lobby Configure(Guid actor, LobbySettings settings)
    {
        RequireHost(actor);
        ArgumentNullException.ThrowIfNull(settings);
        RequireWaiting();
        if (settings.MaximumPlayers < Players.Count)
        {
            throw new LobbyActionException("The player limit cannot be smaller than the current roster.");
        }

        return new Lobby(this) { Settings = settings };
    }

    public Lobby BeginStart(Guid actor)
    {
        RequireHost(actor);
        RequireWaiting();
        if (Players.Count < MinimumPlayers || Players.Count > Settings.MaximumPlayers)
        {
            throw new LobbyActionException("A match needs at least two players within the configured player limit.");
        }

        return new Lobby(this) { Status = LobbyStatus.Starting };
    }

    public Lobby CompleteStart(Guid gameId) => Status == LobbyStatus.Starting
        ? new Lobby(this) { Status = LobbyStatus.Started, GameId = DomainGuard.Id(gameId, nameof(gameId)) }
        : throw new LobbyActionException("The lobby is not starting.");

    public Lobby AbortStart() => Status == LobbyStatus.Starting
        ? new Lobby(this) { Status = LobbyStatus.Waiting } : this;

    private void RequireWaiting()
    {
        if (Status != LobbyStatus.Waiting)
        {
            throw new LobbyActionException("The lobby has already started or is starting.");
        }
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
        RequireWaiting();
        if (Players.Count >= Settings.MaximumPlayers)
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
