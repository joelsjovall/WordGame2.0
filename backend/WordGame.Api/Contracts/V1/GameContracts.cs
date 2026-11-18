namespace WordGame.Api.Contracts.V1;

// Specifications for future authenticated handlers, not mapped endpoints.
public sealed record CreateGameRequest(Guid LobbyId);
public sealed record JoinGameRequest(string LobbyCode);
public sealed record GameCreatedResponse(Guid GameId);
public sealed record JoinGameResponse(Guid LobbyId, string LobbyCode, Guid? GameId);

public enum ApiSessionStatus { Waiting, Starting, Active, Completed, Abandoned }
public enum ApiRoundStatus { CategorySelection, Bidding, Challenge, Completed, Cancelled }
public enum ApiChallengeOutcome { Pending, Succeeded, Failed, Cancelled }
public enum ApiSubmissionStatus { Accepted, Rejected, Duplicate }
public enum ApiEventKind { Updated, Removed }

public sealed record GameStateResponse(
    Guid Id, ApiSessionStatus Status, string Version, DateTimeOffset ServerTime,
    IReadOnlyList<GamePlayerResponse> Players, GameRoundResponse? Round);

public sealed record GamePlayerResponse(Guid Id, string DisplayName, int Score, bool IsConnected);
public sealed record GameRoundResponse(
    Guid Id, int Number, ApiRoundStatus Status, Guid ActivePlayerId, CategoryResponse? Category,
    BidResponse? HighestBid, ChallengeResponse? Challenge);
public sealed record BidResponse(Guid PlayerId, int WordCount);
public sealed record ChallengeResponse(
    Guid Id, Guid PlayerId, Guid CallerId, int RequiredWordCount,
    DateTimeOffset StartedAt, DateTimeOffset Deadline, ApiChallengeOutcome Outcome,
    int AcceptedWordCount, IReadOnlyList<SubmittedWordResponse> Submissions);
public sealed record SubmittedWordResponse(string Text, ApiSubmissionStatus Status);
public sealed record GameActionResponse(Guid ActionId, string Version);
public sealed record GameEventResponse(Guid GameId, string Version, ApiSessionStatus Status, ApiEventKind Kind);
