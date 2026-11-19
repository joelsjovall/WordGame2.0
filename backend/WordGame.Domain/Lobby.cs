namespace WordGame.Domain;

public enum LobbyStatus { Waiting, Starting, Started, Closed }

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
        Roster = new LobbyRoster(host, createdAt);
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; }
    public LobbyCode Code { get; }
    public Guid HostPlayerId => Roster.HostPlayerId;
    public LobbyRoster Roster { get; private init; }
    public LobbyStatus Status { get; private init; } = LobbyStatus.Waiting;
    public LobbySettings Settings { get; private init; } = new();
    public long Version { get; private init; }
    public Guid? GameId { get; private init; }
    public IReadOnlyList<Player> Players => Roster.Players;
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset ExpiresAt { get; }

    private Lobby(Lobby original)
    {
        Id = original.Id;
        Code = original.Code;
        CreatedAt = original.CreatedAt;
        ExpiresAt = original.ExpiresAt;
        Roster = original.Roster;
        Status = original.Status;
        Settings = original.Settings;
        GameId = original.GameId;
        Version = checked(original.Version + 1);
    }

    public void RequireHost(Guid actor)
    {
        Roster.RequireMember(actor);
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

        return new Lobby(this) { Settings = settings, Roster = Roster.ResetReadiness() };
    }

    public Lobby BeginStart(Guid actor)
    {
        RequireHost(actor);
        RequireWaiting();
        if (Players.Count < MinimumPlayers || Players.Count > Settings.MaximumPlayers)
        {
            throw new LobbyActionException("A match needs at least two players within the configured player limit.");
        }
        if (!Roster.AllReady)
        {
            throw new LobbyActionException("Every player must be connected and ready before starting.");
        }

        return new Lobby(this) { Status = LobbyStatus.Starting };
    }

    public Lobby CompleteStart(Guid gameId) => Status == LobbyStatus.Starting && Roster.AllReady
        ? new Lobby(this) { Status = LobbyStatus.Started, GameId = DomainGuard.Id(gameId, nameof(gameId)) }
        : throw new LobbyActionException("Startup was cancelled because a player disconnected. Everyone must ready up again.");

    public Lobby AbortStart() => Status == LobbyStatus.Starting
        ? new Lobby(this) { Status = LobbyStatus.Waiting, Roster = Roster.ResetReadiness() } : this;

    public Lobby SetReady(Guid actor, bool ready)
    {
        Roster.RequireMember(actor);
        RequireWaiting();
        return WithRoster(Roster.SetReady(actor, ready));
    }

    public Lobby Connect(Guid actor, Guid connectionId) => WithRoster(Roster.Connect(actor, connectionId));
    public Lobby Disconnect(Guid actor, Guid connectionId, DateTimeOffset now) => WithRoster(Roster.Disconnect(actor, connectionId, now));

    public Lobby Leave(Guid actor)
    {
        Roster.RequireMember(actor);
        RequireWaiting();
        return WithRoster(Roster.Remove(actor, ban: false));
    }

    public Lobby RemovePlayer(Guid actor, Guid target)
    {
        RequireHost(actor);
        RequireWaiting();
        if (actor == target)
        {
            throw new LobbyActionException("Use Leave lobby to leave as host.");
        }
        DomainGuard.Id(target, nameof(target));
        if (!Roster.Members.ContainsKey(target))
        {
            throw new LobbyActionException("That player is no longer in this lobby.");
        }

        return WithRoster(Roster.Remove(target, ban: true));
    }

    public Lobby PruneDisconnected(DateTimeOffset now, TimeSpan grace)
    {
        if (Status != LobbyStatus.Waiting)
        {
            return this;
        }

        var roster = Roster;
        foreach (var player in Players.Where(player => Roster.Members[player.Id].DisconnectedAt is { } at && now - at >= grace))
        {
            roster = roster.Remove(player.Id, ban: false);
        }

        return WithRoster(roster);
    }

    private Lobby WithRoster(LobbyRoster roster) => ReferenceEquals(roster, Roster) ? this
        : new Lobby(this) { Roster = roster, Status = roster.Players.Count == 0 ? LobbyStatus.Closed : Status };

    private void RequireWaiting()
    {
        if (Status != LobbyStatus.Waiting)
        {
            throw new LobbyActionException("The lobby has already started or is starting.");
        }
    }

    public Lobby Join(Player player, DateTimeOffset? now = null)
    {
        ArgumentNullException.ThrowIfNull(player);
        ValidateName(player);
        if (Roster.WasRemoved(player.Id))
        {
            throw new LobbyActionException("You have been removed from this lobby.", forbidden: true);
        }

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

        return WithRoster(Roster.Join(player, now ?? CreatedAt));
    }

    private static void ValidateName(Player player)
    {
        if (player.DisplayName.Length > MaximumDisplayNameLength || player.DisplayName.Any(char.IsControl))
        {
            throw new ArgumentException("Display names must contain at most 32 characters and no control characters.", nameof(player));
        }
    }
}
