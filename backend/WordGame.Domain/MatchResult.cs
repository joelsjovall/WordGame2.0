namespace WordGame.Domain;

public sealed record MatchStanding(Guid PlayerId, string DisplayName, int Score, int Rank);

public sealed class MatchResult
{
    internal MatchResult(Game game)
    {
        GameId = game.Id;
        Format = game.Settings.Format;
        CompletedRoundCount = game.Rounds.Count;
        // OrderBy is stable: tied players retain roster order without breaking the tie.
        var ordered = game.Players.OrderByDescending(player => game.Scores[player.Id].Points).ToArray();
        Standings = Array.AsReadOnly(ordered.Select(player => new MatchStanding(player.Id, player.DisplayName,
            game.Scores[player.Id].Points, 1 + ordered.Count(other => game.Scores[other.Id].Points > game.Scores[player.Id].Points))).ToArray());
        WinnerIds = Array.AsReadOnly(Standings.Where(standing => standing.Rank == 1).Select(standing => standing.PlayerId).ToArray());
    }

    public Guid GameId { get; }
    public MatchFormat Format { get; }
    public int CompletedRoundCount { get; }
    public IReadOnlyList<MatchStanding> Standings { get; }
    public IReadOnlyList<Guid> WinnerIds { get; }
    public bool IsTie => WinnerIds.Count > 1;
}
