using WordGame.Api.Contracts.V1;
using WordGame.Domain;

namespace WordGame.Api.Mapping;

internal static class LobbyMapper
{
    internal static LobbyResponse Map(Lobby lobby, DateTimeOffset serverTime) => new(lobby.Id, lobby.Code.Value, lobby.HostPlayerId, lobby.Status switch
    {
        LobbyStatus.Waiting => ApiLobbyStatus.Waiting,
        LobbyStatus.Starting => ApiLobbyStatus.Starting,
        LobbyStatus.Started => ApiLobbyStatus.Started,
        LobbyStatus.Completed => ApiLobbyStatus.Completed,
        _ => ApiLobbyStatus.Closed
    },
        lobby.Players.Select(player => new LobbyPlayerResponse(player.Id, player.DisplayName,
            lobby.Roster.Members[player.Id].IsReady, lobby.Roster.Members[player.Id].IsConnected)).ToArray(),
        Lobby.MinimumPlayers, lobby.Settings.MaximumPlayers, lobby.CreatedAt, lobby.ExpiresAt,
        lobby.Version.ToString(System.Globalization.CultureInfo.InvariantCulture),
        new LobbySettingsRequest(lobby.Settings.MaximumPlayers, lobby.Settings.RoundCount, lobby.Settings.ChallengeSeconds,
            lobby.Settings.AllowedDifficulties.Select(value => Enum.Parse<ApiCategoryDifficulty>(value.ToString())).ToArray(),
            lobby.Settings.AllowedCategoryIds, lobby.Settings.SealedOpening), lobby.GameId,
        lobby.Status == LobbyStatus.Waiting && lobby.Roster.AllReady && lobby.Players.Count <= lobby.Settings.MaximumPlayers,
        lobby.StartsAt, serverTime, lobby.LastResult is { } result ? GameStateMapper.MapMatchResult(result) : null);
}
