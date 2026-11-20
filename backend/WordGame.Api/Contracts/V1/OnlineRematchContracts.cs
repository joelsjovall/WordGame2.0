namespace WordGame.Api.Contracts.V1;

public enum ApiRematchStatus { Idle, Pending, Ready, Declined, Expired, Unavailable }
public enum ApiRematchAction { Request, Accept, Decline }
public sealed record OnlineRematchRequest(ApiRematchAction Action);
public sealed record OnlineRematchResponse(Guid SourceGameId, ApiRematchStatus Status, IReadOnlyList<Guid> AcceptedPlayerIds,
    DateTimeOffset? ExpiresAt, Guid? GameId);
