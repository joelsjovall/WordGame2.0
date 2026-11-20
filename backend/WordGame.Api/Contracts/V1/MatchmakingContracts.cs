namespace WordGame.Api.Contracts.V1;

public sealed record JoinMatchmakingRequest(string DisplayName);
public sealed record MatchAcceptanceRequest;
public enum ApiMatchmakingStatus { Idle, Searching, Matched, Ready, Failed, Cancelled, Expired }
public sealed record MatchmakingResponse(Guid? SearchId, ApiMatchmakingStatus Status, DateTimeOffset? CreatedAt,
    DateTimeOffset? ExpiresAt, Guid? MatchId, Guid? GameId, bool HasAccepted);
