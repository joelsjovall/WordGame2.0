using System.Collections.Frozen;

namespace WordGame.Domain;

public sealed record LobbyMember(bool IsReady, IReadOnlySet<Guid> Connections, DateTimeOffset? DisconnectedAt)
{
    public bool IsConnected => Connections.Count > 0;
}

// Immutable membership/presence state; connection IDs distinguish tabs without allocating extra player slots.
public sealed class LobbyRoster
{
    private readonly IReadOnlySet<Guid> removed;

    internal LobbyRoster(Player host, DateTimeOffset now) : this([host], host.Id,
        new Dictionary<Guid, LobbyMember> { [host.Id] = new(false, Array.Empty<Guid>().ToFrozenSet(), now) }, [])
    { }

    private LobbyRoster(IEnumerable<Player> players, Guid host, IReadOnlyDictionary<Guid, LobbyMember> members, IEnumerable<Guid> removed)
    {
        Players = Array.AsReadOnly(players.ToArray());
        HostPlayerId = host;
        Members = members.ToFrozenDictionary();
        this.removed = removed.ToFrozenSet();
    }

    public IReadOnlyList<Player> Players { get; }
    public Guid HostPlayerId { get; }
    public IReadOnlyDictionary<Guid, LobbyMember> Members { get; }
    public bool AllReady => Players.Count >= Game.MinimumPlayers && Members.Values.All(member => member.IsReady && member.IsConnected);
    public bool WasRemoved(Guid playerId) => removed.Contains(playerId);

    public LobbyMember RequireMember(Guid playerId) => Members.TryGetValue(playerId, out var member) ? member
        : throw new LobbyActionException("You are no longer a member of this lobby.", forbidden: true);

    public LobbyRoster Join(Player player, DateTimeOffset now)
    {
        var members = ResetReady().ToDictionary();
        members.Add(player.Id, new(false, Array.Empty<Guid>().ToFrozenSet(), now));
        return new(Players.Append(player), HostPlayerId, members, removed);
    }

    public LobbyRoster SetReady(Guid actor, bool ready)
    {
        var member = RequireMember(actor);
        if (ready && !member.IsConnected)
        {
            throw new LobbyActionException("Connect to the lobby before marking yourself ready.");
        }

        return member.IsReady == ready ? this : Replace(actor, member with { IsReady = ready });
    }

    public LobbyRoster Connect(Guid actor, Guid connectionId)
    {
        DomainGuard.Id(connectionId, nameof(connectionId));
        var member = RequireMember(actor);
        if (member.Connections.Contains(connectionId))
        {
            return this;
        }

        return Replace(actor, member with { Connections = member.Connections.Append(connectionId).ToFrozenSet(), DisconnectedAt = null });
    }

    public LobbyRoster Disconnect(Guid actor, Guid connectionId, DateTimeOffset now)
    {
        if (!Members.TryGetValue(actor, out var member) || !member.Connections.Contains(connectionId))
        {
            return this;
        }

        var connections = member.Connections.Where(value => value != connectionId).ToFrozenSet();
        return Replace(actor, member with
        {
            Connections = connections,
            IsReady = connections.Count > 0 && member.IsReady,
            DisconnectedAt = connections.Count == 0 ? now : null
        });
    }

    public LobbyRoster Remove(Guid playerId, bool ban)
    {
        RequireMember(playerId);
        var players = Players.Where(player => player.Id != playerId).ToArray();
        var members = ResetReady().Where(pair => pair.Key != playerId).ToDictionary();
        var host = HostPlayerId == playerId
            ? (players.FirstOrDefault(player => members[player.Id].IsConnected) ?? players.FirstOrDefault())?.Id ?? Guid.Empty
            : HostPlayerId;
        return new(players, host, members, ban ? removed.Append(playerId) : removed);
    }

    public LobbyRoster ResetReadiness() => new(Players, HostPlayerId, ResetReady(), removed);
    private IReadOnlyDictionary<Guid, LobbyMember> ResetReady() => Members.ToDictionary(pair => pair.Key, pair => pair.Value with { IsReady = false });
    private LobbyRoster Replace(Guid playerId, LobbyMember member)
    {
        var members = Members.ToDictionary();
        members[playerId] = member;
        return new(Players, HostPlayerId, members, removed);
    }
}
