namespace WordGame.Api.Contracts.V1;

public sealed record CreateGuestRequest;
public sealed record GuestIdentityResponse(Guid PlayerId);
public sealed record CreateLobbyRequest(Guid RequestId, string DisplayName);
public sealed record JoinLobbyRequest(string LobbyCode, string DisplayName, Guid? LobbyId = null);
public enum ApiLobbyStatus { Waiting, Starting, Started, Closed, Completed }
public sealed record LobbySettingsRequest(int MaximumPlayers, int? RoundCount, int ChallengeSeconds,
    IReadOnlyList<ApiCategoryDifficulty> AllowedDifficulties, IReadOnlyList<string>? AllowedCategoryIds = null);
public sealed record UpdateLobbySettingsRequest(string ExpectedVersion, LobbySettingsRequest Settings);
public sealed record StartLobbyRequest(string ExpectedVersion);
public sealed record SetLobbyReadyRequest(string ExpectedVersion, bool IsReady);
public sealed record LeaveLobbyRequest;
public sealed record RemoveLobbyPlayerRequest(Guid PlayerId);
public sealed record LobbyPlayerResponse(Guid Id, string DisplayName, bool IsReady, bool IsConnected);
public sealed record LobbyResponse(Guid Id, string LobbyCode, Guid HostPlayerId, ApiLobbyStatus Status,
    IReadOnlyList<LobbyPlayerResponse> Players, int MinimumPlayers, int MaximumPlayers,
    DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, string Version, LobbySettingsRequest Settings, Guid? GameId, bool CanStart,
    DateTimeOffset? StartsAt, DateTimeOffset ServerTime, MatchResultResponse? LastResult);
