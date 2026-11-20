namespace WordGame.Api.Contracts.V1;

public sealed record OnlineStatisticsResponse(int MatchesPlayed, int Wins, int Losses, int Draws,
    double WinPercentage, int SuccessfulBluffChallenges, int FailedBluffChallenges);
public sealed record OnlineHistoryPlayerResponse(Guid PlayerId, string DisplayName, int Score, bool Won, bool Draw,
    int SuccessfulBluffChallenges, int FailedBluffChallenges);
public sealed record OnlineHistoryMatchResponse(Guid MatchId, DateTimeOffset CompletedAt, ApiMatchEndReason Reason,
    IReadOnlyList<OnlineHistoryPlayerResponse> Players);
public sealed record OnlineHistoryResponse(IReadOnlyList<OnlineHistoryMatchResponse> Items);
