namespace WordGame.Api.Contracts.V1;

public sealed record CreateGuestRequest;
public sealed record GuestIdentityResponse(Guid PlayerId);
public sealed record CreateLobbyRequest(Guid RequestId, string DisplayName);
public sealed record JoinLobbyRequest(string LobbyCode, string DisplayName, Guid? LobbyId = null);
public enum ApiLobbyStatus { Waiting, Starting, Started }
public sealed record LobbySettingsRequest(int MaximumPlayers, int? RoundCount, int ChallengeSeconds, IReadOnlyList<ApiCategoryDifficulty> AllowedDifficulties);
public sealed record UpdateLobbySettingsRequest(string ExpectedVersion, LobbySettingsRequest Settings);
public sealed record StartLobbyRequest(string ExpectedVersion);
public sealed record LobbyPlayerResponse(Guid Id, string DisplayName);
public sealed record LobbyResponse(Guid Id, string LobbyCode, Guid HostPlayerId, ApiLobbyStatus Status,
    IReadOnlyList<LobbyPlayerResponse> Players, int MinimumPlayers, int MaximumPlayers,
    DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, string Version, LobbySettingsRequest Settings, Guid? GameId);
