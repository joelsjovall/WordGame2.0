namespace WordGame.Api.Contracts.V1;

public enum ApiSessionStatus { Waiting, Starting, Active, Completed, Abandoned }
public enum ApiRoundStatus { CategorySelection, Bidding, Challenge, Completed, Cancelled }
public enum ApiChallengeOutcome { Pending, Succeeded, Failed, Cancelled }
public enum ApiSubmissionStatus { Accepted, Rejected, Duplicate }
public enum ApiEventKind { Updated, Removed }
public enum ApiMatchFormat { Party, Online }

public sealed record GameStateResponse(
    Guid Id, ApiSessionStatus Status, string Version, DateTimeOffset ServerTime,
    IReadOnlyList<GamePlayerResponse> Players, GameRoundResponse? Round,
    MatchSettingsResponse Match, MatchResultResponse? Result);

public sealed record MatchSettingsResponse(ApiMatchFormat Format, int RoundCount, double? ChallengeSeconds = null,
    IReadOnlyList<ApiCategoryDifficulty>? AllowedDifficulties = null);
public sealed record MatchResultResponse(Guid GameId, ApiMatchFormat Format, int CompletedRoundCount,
    bool IsTie, IReadOnlyList<Guid> WinnerIds, IReadOnlyList<MatchStandingResponse> Standings);
public sealed record MatchStandingResponse(Guid PlayerId, string DisplayName, int Score, int Rank);

public sealed record GamePlayerResponse(Guid Id, string DisplayName, int Score, bool IsConnected);
public sealed record GameRoundResponse(
    Guid Id, int Number, ApiRoundStatus Status, Guid ActivePlayerId, CategoryResponse? Category,
    BidResponse? HighestBid, ChallengeResponse? Challenge, RoundResultResponse? Result = null,
    DateTimeOffset? CategorySelectionDeadline = null);
public sealed record RoundResultResponse(
    Guid RoundId, int RoundNumber, CategoryResponse Category, Guid ChallengeId,
    Guid BidderId, Guid CallerId, Guid WinnerId, int RequiredWordCount, int AcceptedWordCount,
    ApiChallengeOutcome Outcome, IReadOnlyList<SubmittedWordResponse> Submissions, IReadOnlyList<RoundPlayerScoreResponse> Scores);
public sealed record RoundPlayerScoreResponse(Guid PlayerId, int PointsAwarded, int TotalPoints);
public sealed record BidResponse(Guid PlayerId, int WordCount);
public sealed record ChallengeResponse(
    Guid Id, Guid PlayerId, Guid CallerId, int RequiredWordCount,
    DateTimeOffset StartedAt, DateTimeOffset Deadline, ApiChallengeOutcome Outcome,
    int AcceptedWordCount, IReadOnlyList<SubmittedWordResponse> Submissions);
public sealed record SubmittedWordResponse(string Text, ApiSubmissionStatus Status);
public sealed record GameActionResponse(Guid ActionId, string Version);
public sealed record GameEventResponse(Guid GameId, string Version, ApiSessionStatus Status, ApiEventKind Kind);
