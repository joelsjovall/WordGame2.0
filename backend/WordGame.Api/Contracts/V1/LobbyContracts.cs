namespace WordGame.Api.Contracts.V1;

public sealed record CreateGuestRequest;
public sealed record GuestIdentityResponse(Guid PlayerId);
public sealed record CreateLobbyRequest(Guid RequestId, string DisplayName);
public enum ApiLobbyStatus { Waiting }
public sealed record LobbyPlayerResponse(Guid Id, string DisplayName);
public sealed record LobbyResponse(Guid Id, string LobbyCode, Guid HostPlayerId, ApiLobbyStatus Status,
    IReadOnlyList<LobbyPlayerResponse> Players, int MinimumPlayers, int MaximumPlayers,
    DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt);
