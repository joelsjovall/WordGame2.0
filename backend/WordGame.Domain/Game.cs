using System.Collections.ObjectModel;

namespace WordGame.Domain;

public sealed class Game
{
    private readonly List<GameRound> rounds = [];
    private readonly Dictionary<Guid, PlayerScore> scores;
    private readonly Dictionary<Guid, RoundResult> roundResults = [];

    public Game(Guid id, IEnumerable<Player> players)
    {
        Id = DomainGuard.Id(id, nameof(id));
        ArgumentNullException.ThrowIfNull(players);
        var roster = players.ToArray();
        if (roster.Length is < 2 or > 4 || roster.Any(player => player is null))
        {
            throw new ArgumentException("A game needs two to four non-null players.", nameof(players));
        }

        if (roster.Select(player => player.Id).Distinct().Count() != roster.Length)
        {
            throw new ArgumentException("Player IDs must be unique within the game.", nameof(players));
        }

        Players = Array.AsReadOnly(roster);
        Rounds = rounds.AsReadOnly();
        scores = roster.ToDictionary(player => player.Id, player => new PlayerScore(player.Id, 0));
        Scores = new ReadOnlyDictionary<Guid, PlayerScore>(scores);
        RoundResults = new ReadOnlyDictionary<Guid, RoundResult>(roundResults);
    }

    public Guid Id { get; }
    // Roster order is the stable turn-order basis shared by both modes.
    public IReadOnlyList<Player> Players { get; }
    public IReadOnlyList<GameRound> Rounds { get; }
    public IReadOnlyDictionary<Guid, PlayerScore> Scores { get; }
    public IReadOnlyDictionary<Guid, RoundResult> RoundResults { get; }
    public GameStatus Status { get; private set; } = GameStatus.WaitingToStart;
    public GameRound? CurrentRound => rounds.LastOrDefault();

    // Copies mutable aggregates for isolated session transactions and detached reads.
    public Game Copy()
    {
        var copy = new Game(Id, Players) { Status = Status };
        copy.rounds.AddRange(rounds.Select(round => round.Copy()));
        foreach (var score in scores)
        {
            copy.scores[score.Key] = score.Value;
        }

        foreach (var result in roundResults)
        {
            copy.roundResults.Add(result.Key, result.Value);
        }
        return copy;
    }

    // Pure roster lookup; the engine decides when to advance a turn or start a round.
    public Guid GetNextPlayerId(Guid currentPlayerId)
    {
        for (var index = 0; index < Players.Count; index++)
        {
            if (Players[index].Id == currentPlayerId)
            {
                return Players[(index + 1) % Players.Count].Id;
            }
        }

        throw new ArgumentException("The current player must belong to this game.", nameof(currentPlayerId));
    }

    public GameRound StartRound(Guid roundId, Guid startingPlayerId)
    {
        DomainGuard.State(Status is GameStatus.WaitingToStart or GameStatus.InProgress,
            "The game has already ended.");
        DomainGuard.State(CurrentRound is null || CurrentRound.Status == RoundStatus.Completed,
            "The previous round must be completed.");
        DomainGuard.State(!rounds.Any(round => round.Id == roundId), "Round IDs must be unique within a game.");

        var round = new GameRound(roundId, checked(rounds.Count + 1), Players.Select(player => player.Id), startingPlayerId);
        rounds.Add(round);
        Status = GameStatus.InProgress;
        return round;
    }

    // ScoringRules calculates awards; this aggregate enforces atomic, once-per-round totals.
    public void RecordRoundScores(Guid roundId, IEnumerable<PlayerScore> awards)
    {
        ArgumentNullException.ThrowIfNull(awards);
        DomainGuard.State(Status == GameStatus.InProgress, "Scoring requires an active game.");
        DomainGuard.State(rounds.Any(round => round.Id == roundId && round.Status == RoundStatus.Completed),
            "Only completed rounds can award points.");
        DomainGuard.State(!roundResults.ContainsKey(roundId), "This round has already awarded points.");
        var changes = awards.ToArray();
        DomainGuard.State(changes.All(award => award is not null && scores.ContainsKey(award.PlayerId)),
            "All awards must refer to players in this game.");
        DomainGuard.State(changes.Select(award => award.PlayerId).Distinct().Count() == changes.Length,
            "A player can receive only one award entry per round.");

        // Compute everything first so an invalid award or overflow cannot partially update scores.
        var updated = changes.Select(award =>
            new PlayerScore(award.PlayerId, checked(scores[award.PlayerId].Points + award.Points))).ToArray();
        var awardsByPlayer = changes.ToDictionary(award => award.PlayerId, award => award.Points);
        var totalsByPlayer = updated.ToDictionary(score => score.PlayerId, score => score.Points);
        var result = new RoundResult(rounds.Single(round => round.Id == roundId), Players.Select(player =>
            new RoundPlayerScore(player.Id, awardsByPlayer.GetValueOrDefault(player.Id),
                totalsByPlayer.GetValueOrDefault(player.Id, scores[player.Id].Points))).ToArray());
        foreach (var score in updated)
        {
            scores[score.PlayerId] = score;
        }

        roundResults.Add(roundId, result);
    }

    public void Complete()
    {
        DomainGuard.State(Status == GameStatus.InProgress && CurrentRound?.Status == RoundStatus.Completed,
            "An active game needs a completed final round.");
        Status = GameStatus.Completed;
    }

    public void Cancel()
    {
        DomainGuard.State(Status is GameStatus.WaitingToStart or GameStatus.InProgress,
            "The game has already ended.");
        if (CurrentRound is { Status: not (RoundStatus.Completed or RoundStatus.Cancelled) } round)
        {
            round.Cancel();
        }

        Status = GameStatus.Cancelled;
    }
}
