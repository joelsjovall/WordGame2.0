namespace WordGame.Domain;

public sealed record MatchStanding(Guid PlayerId, string DisplayName, int Score, int Rank);
public sealed record BluffHighlight(string CategoryName, int WordCount, string BidderName, string CallerName);
public enum MatchEndReason { Completed, Forfeit, ReconnectionExpired }

public sealed class MatchResult
{
    internal MatchResult(Game game, MatchEndReason reason = MatchEndReason.Completed, Guid? forfeitedPlayerId = null)
    {
        GameId = game.Id;
        Format = game.Settings.Format;
        CompletedRoundCount = game.RoundResults.Count;
        Reason = reason;
        ForfeitedPlayerId = forfeitedPlayerId;
        // OrderBy is stable: tied players retain roster order without breaking the tie.
        var ordered = forfeitedPlayerId is { } loser ? game.Players.OrderBy(player => player.Id == loser).ToArray()
            : game.Players.OrderByDescending(player => game.Scores[player.Id].Points).ToArray();
        Standings = Array.AsReadOnly(ordered.Select((player, index) => new MatchStanding(player.Id, player.DisplayName,
            game.Scores[player.Id].Points, forfeitedPlayerId.HasValue ? index + 1
                : 1 + ordered.Count(other => game.Scores[other.Id].Points > game.Scores[player.Id].Points))).ToArray());
        WinnerIds = Array.AsReadOnly(Standings.Where(standing => standing.Rank == 1).Select(standing => standing.PlayerId).ToArray());
        var bestCall = game.RoundResults.Values
            .Where(round => round.Outcome == ChallengeOutcome.Failed)
            .OrderByDescending(round => round.RequiredWordCount)
            .ThenByDescending(round => round.RoundNumber)
            .FirstOrDefault();
        BestBluffCall = bestCall is null ? null : new BluffHighlight(bestCall.Category.Name,
            bestCall.RequiredWordCount,
            game.Players.Single(player => player.Id == bestCall.BidderId).DisplayName,
            game.Players.Single(player => player.Id == bestCall.CallerId).DisplayName);
    }

    public Guid GameId { get; }
    public MatchFormat Format { get; }
    public int CompletedRoundCount { get; }
    public IReadOnlyList<MatchStanding> Standings { get; }
    public IReadOnlyList<Guid> WinnerIds { get; }
    public bool IsTie => WinnerIds.Count > 1;
    public MatchEndReason Reason { get; }
    public Guid? ForfeitedPlayerId { get; }
    public BluffHighlight? BestBluffCall { get; }
}
