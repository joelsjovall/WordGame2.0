using WordGame.Api.Contracts.V1;
using WordGame.Domain;

namespace WordGame.Api.Mapping;

internal static class LobbyMapper
{
    internal static LobbyResponse Map(Lobby lobby) => new(lobby.Id, lobby.Code.Value, lobby.HostPlayerId, ApiLobbyStatus.Waiting,
        lobby.Players.Select(player => new LobbyPlayerResponse(player.Id, player.DisplayName)).ToArray(),
        Lobby.MinimumPlayers, Lobby.MaximumPlayers, lobby.CreatedAt, lobby.ExpiresAt);
}
