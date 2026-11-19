namespace WordGame.Domain;

public enum LobbyStatus { Waiting, Starting, Started, Closed, Completed }

// A pregame lobby can have one host; the gameplay engine still requires a fixed roster of 2–4.
public sealed class Lobby
{
    public const int MinimumPlayers = Game.MinimumPlayers;
    public const int MaximumPlayers = Game.MaximumPlayers;
    public const int MaximumDisplayNameLength = Player.MaximumDisplayNameLength;

    public Lobby(Guid id, LobbyCode code, Player host, DateTimeOffset createdAt, DateTimeOffset expiresAt)
    {
        Id = DomainGuard.Id(id, nameof(id));
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(host);
        host.ValidateDisplayName();
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
    public DateTimeOffset? StartsAt { get; private init; }
    public MatchResult? LastResult { get; private init; }
    public IReadOnlyList<Player> Players => Roster.Players;
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset ExpiresAt { get; private init; }

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
        StartsAt = original.StartsAt;
        LastResult = original.LastResult;
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

    public Lobby BeginStart(Guid actor, DateTimeOffset? startsAt = null)
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

        return new Lobby(this) { Status = LobbyStatus.Starting, StartsAt = startsAt, LastResult = null };
    }

    public Lobby CompleteStart(Guid gameId, DateTimeOffset? now = null)
    {
        if (Status != LobbyStatus.Starting || !Roster.AllReady)
        {
            throw new LobbyActionException("Startup was cancelled because a player disconnected. Everyone must ready up again.");
        }

        if (StartsAt is not null && (now is null || now < StartsAt))
        {
            throw new LobbyActionException("The start countdown has not finished.");
        }

        return new Lobby(this) { Status = LobbyStatus.Started, GameId = DomainGuard.Id(gameId, nameof(gameId)) };
    }

    public Lobby AbortStart() => Status == LobbyStatus.Starting
        ? new Lobby(this) { Status = LobbyStatus.Waiting, StartsAt = null, Roster = Roster.ResetReadiness() } : this;

    public Lobby FinishMatch(Guid gameId, MatchResult? result, DateTimeOffset now, TimeSpan lifetime)
    {
        if (Status != LobbyStatus.Started || GameId != gameId)
        {
            return this;
        }

        if (result is not null && result.GameId != gameId)
        {
            throw new ArgumentException("The result belongs to another match.");
        }

        if (lifetime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(lifetime));
        }

        return new Lobby(this)
        {
            Status = LobbyStatus.Completed,
            LastResult = result,
            ExpiresAt = now.Add(lifetime),
            Roster = Roster.ResetReadiness()
        };
    }

    public Lobby PrepareRematch(Guid actor)
    {
        RequireHost(actor);
        if (Status != LobbyStatus.Completed)
        {
            throw new LobbyActionException("A rematch requires an ended match.");
        }

        return new Lobby(this) { Status = LobbyStatus.Waiting, GameId = null, StartsAt = null, Roster = Roster.ResetReadiness() };
    }

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
        if (Status is not (LobbyStatus.Waiting or LobbyStatus.Completed))
        {
            throw new LobbyActionException("Wait for the match to end before leaving the lobby.");
        }

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
        if (Status is not (LobbyStatus.Waiting or LobbyStatus.Completed))
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
        player.ValidateDisplayName();
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

}
