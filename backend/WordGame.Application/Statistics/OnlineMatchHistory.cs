using WordGame.Application.Sessions;
using WordGame.Domain;

namespace WordGame.Application.Statistics;

public sealed record OnlinePlayerResult(Guid PlayerId, string DisplayName, int Score, bool Won, bool Draw,
    int SuccessfulBluffChallenges, int FailedBluffChallenges);

public sealed record OnlineMatchHistory(Guid MatchId, DateTimeOffset CompletedAt, MatchEndReason Reason,
    IReadOnlyList<OnlinePlayerResult> Players)
{
    public static OnlineMatchHistory? From(SessionSnapshot session)
    {
        if (session.Status != SessionStatus.Completed || session.Game.Result is not { Format: MatchFormat.Online } result)
        {
            return null;
        }
        return new(session.Id, session.EndedAt!.Value, result.Reason, result.Standings.Select(standing =>
            new OnlinePlayerResult(standing.PlayerId, standing.DisplayName, standing.Score,
                !result.IsTie && result.WinnerIds.Contains(standing.PlayerId), result.IsTie,
                session.Game.RoundResults.Values.Count(round => round.CallerId == standing.PlayerId && round.Outcome == ChallengeOutcome.Failed),
                session.Game.RoundResults.Values.Count(round => round.CallerId == standing.PlayerId && round.Outcome == ChallengeOutcome.Succeeded))).ToArray());
    }
}

public sealed record OnlinePlayerStatistics(int MatchesPlayed, int Wins, int Losses, int Draws,
    double WinPercentage, int SuccessfulBluffChallenges, int FailedBluffChallenges)
{
    public static OnlinePlayerStatistics Calculate(Guid playerId, IEnumerable<OnlineMatchHistory> matches)
    {
        var results = matches.SelectMany(match => match.Players.Where(player => player.PlayerId == playerId)).ToArray();
        var wins = results.Count(player => player.Won);
        var draws = results.Count(player => player.Draw);
        return new(results.Length, wins, results.Length - wins - draws, draws,
            results.Length == 0 ? 0 : Math.Round(100d * wins / results.Length, 2),
            results.Sum(player => player.SuccessfulBluffChallenges), results.Sum(player => player.FailedBluffChallenges));
    }
}
