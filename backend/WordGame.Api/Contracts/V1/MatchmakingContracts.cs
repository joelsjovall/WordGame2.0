namespace WordGame.Api.Contracts.V1;

public sealed record JoinMatchmakingRequest(string DisplayName);
public enum ApiMatchmakingStatus { Idle, Searching, Matched, Cancelled, Expired }
public sealed record MatchmakingResponse(Guid? SearchId, ApiMatchmakingStatus Status, DateTimeOffset? CreatedAt,
    DateTimeOffset? ExpiresAt, Guid? MatchId, Guid? GameId);
