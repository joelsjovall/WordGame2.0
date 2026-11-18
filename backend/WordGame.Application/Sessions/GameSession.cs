using WordGame.Application.Categories;
using WordGame.Application.Games;
using WordGame.Domain;

namespace WordGame.Application.Sessions;

public sealed class GameSession
{
    private readonly Dictionary<Guid, HashSet<Guid>> connections;
    private readonly Dictionary<Guid, DateTimeOffset?> disconnectedAt;
    private readonly HashSet<Guid> departed = [];
    private readonly HashSet<Guid> processedActions = [];

    public GameSession(Guid id, IEnumerable<Player> players, DateTimeOffset createdAt)
    {
        Game = new Game(id, players);
        CreatedAt = UpdatedAt = createdAt;
        AllDisconnectedSince = createdAt;
        connections = Game.Players.ToDictionary(player => player.Id, _ => new HashSet<Guid>());
        disconnectedAt = Game.Players.ToDictionary(player => player.Id, _ => (DateTimeOffset?)createdAt);
    }

    private GameSession(GameSession original, DateTimeOffset now)
    {
        Game = original.Game.Copy();
        Status = original.Status;
        CreatedAt = original.CreatedAt;
        UpdatedAt = now;
        EndedAt = original.EndedAt;
        AllDisconnectedSince = original.AllDisconnectedSince;
        connections = original.connections.ToDictionary(pair => pair.Key, pair => new HashSet<Guid>(pair.Value));
        disconnectedAt = new Dictionary<Guid, DateTimeOffset?>(original.disconnectedAt);
        departed.UnionWith(original.departed);
        processedActions.UnionWith(original.processedActions);
        CategorySelectionDeadline = original.CategorySelectionDeadline;
        SelectedCategory = original.SelectedCategory;
    }

    public Guid Id => Game.Id;
    public Game Game { get; }
    public SessionStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; }
    public DateTimeOffset? EndedAt { get; private set; }
    public DateTimeOffset? AllDisconnectedSince { get; private set; }
    public DateTimeOffset? CategorySelectionDeadline { get; private set; }
    public RoundCategorySelection? SelectedCategory { get; private set; }

    public GameSession Copy(DateTimeOffset now) => new(this, now);

    public SessionSnapshot Snapshot(long version) => new(Id, Status, version, CreatedAt, UpdatedAt, EndedAt,
        Array.AsReadOnly(Game.Players.Select(player =>
            new PlayerPresence(player.Id, connections[player.Id].Count, disconnectedAt[player.Id], departed.Contains(player.Id))).ToArray()),
        Game.Copy(), CategorySelectionDeadline, SelectedCategory);

    internal void BeginCategorySelection(TimeSpan? timeout)
    {
        Require(Game.CurrentRound?.Status == RoundStatus.CategorySelection, "A new category-selection round is required.");
        CategorySelectionDeadline = timeout is { } duration ? UpdatedAt.Add(duration) : null;
        SelectedCategory = null;
    }

    internal void SelectCategory(Guid playerId, CategoryDefinition definition, int multiplier)
    {
        var round = Game.CurrentRound ?? throw new InvalidOperationException("A round is required.");
        round.SelectCategory(playerId, definition.Category);
        SelectedCategory = new RoundCategorySelection(round.Id, definition, multiplier);
        CategorySelectionDeadline = null;
    }

    public void BeginStarting()
    {
        Require(Status == SessionStatus.Waiting, "Only a waiting session can start.");
        Status = SessionStatus.Starting;
    }

    public void Activate()
    {
        Require(Status == SessionStatus.Starting && Game.Status == GameStatus.InProgress,
            "Activation requires a starting session and an initialized game.");
        Status = SessionStatus.Active;
    }

    public void Complete()
    {
        Require(Status == SessionStatus.Active && Game.Status == GameStatus.Completed,
            "Completion requires an active session and a completed game.");
        Status = SessionStatus.Completed;
        CategorySelectionDeadline = null;
        EndedAt = UpdatedAt;
    }

    public void Abandon()
    {
        Require(Status is not (SessionStatus.Completed or SessionStatus.Abandoned), "The session has already ended.");
        if (Game.Status != GameStatus.Cancelled)
        {
            Game.Cancel();
        }

        Status = SessionStatus.Abandoned;
        CategorySelectionDeadline = null;
        EndedAt = UpdatedAt;
    }

    public void Connect(Guid playerId, Guid connectionId)
    {
        Require(Status != SessionStatus.Abandoned, "An abandoned session cannot reconnect.");
        Require(connections.ContainsKey(playerId), "The player must belong to this session.");
        Require(!departed.Contains(playerId), "A player who left this match cannot reconnect.");
        if (connectionId == Guid.Empty)
        {
            throw new ArgumentException("Connection IDs must not be empty.", nameof(connectionId));
        }

        Require(!connections.Any(pair => pair.Key != playerId && pair.Value.Contains(connectionId)),
            "A connection cannot belong to multiple players.");
        connections[playerId].Add(connectionId);
        disconnectedAt[playerId] = null;
        AllDisconnectedSince = null;
    }

    public void Disconnect(Guid playerId, Guid connectionId)
    {
        Require(connections.ContainsKey(playerId), "The player must belong to this session.");
        if (!connections[playerId].Remove(connectionId) || connections[playerId].Count > 0)
        {
            return;
        }

        disconnectedAt[playerId] = UpdatedAt;
        if (connections.Values.All(ids => ids.Count == 0))
        {
            AllDisconnectedSince = UpdatedAt;
        }
    }

    public void EnsureConsistent()
    {
        var expected = Status switch
        {
            SessionStatus.Waiting or SessionStatus.Starting => GameStatus.WaitingToStart,
            SessionStatus.Active => GameStatus.InProgress,
            SessionStatus.Completed => GameStatus.Completed,
            SessionStatus.Abandoned => GameStatus.Cancelled,
            _ => throw new InvalidOperationException("Unknown session status.")
        };
        Require(Game.Status == expected, "Session and game lifecycle states must agree before commit.");
        if (SelectedCategory is { } selected)
        {
            Require(Game.CurrentRound?.Id == selected.RoundId && Game.CurrentRound.Category == selected.Definition.Category,
                "The selected answer snapshot must match the current round.");
        }
    }

    internal bool HasProcessedAction(Guid actionId) => processedActions.Contains(actionId);
    internal void RecordAction(Guid actionId) => processedActions.Add(actionId);
    internal bool HasLeft(Guid playerId) => departed.Contains(playerId);
    internal bool CanTakeTurn(Guid playerId, TimeSpan grace) => !departed.Contains(playerId) &&
        (connections[playerId].Count > 0 || disconnectedAt[playerId] is { } since && UpdatedAt - since < grace);

    internal void Leave(Guid playerId)
    {
        Require(connections.ContainsKey(playerId) && !departed.Contains(playerId), "The player is not eligible to leave.");
        departed.Add(playerId);
        connections[playerId].Clear();
        disconnectedAt[playerId] = UpdatedAt;
        if (connections.Values.All(ids => ids.Count == 0))
        {
            AllDisconnectedSince = UpdatedAt;
        }

        if (Status == SessionStatus.Waiting && Game.Players.Count(player => !HasLeft(player.Id)) < 2)
        {
            Abandon();
        }
    }

    // Used by engine presence operations and the periodic cleanup sweep, using this draft's timestamp.
    public bool ReconcileTurns(TimeSpan disconnectedGrace) => TurnPolicy.Reconcile(this, disconnectedGrace);

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
