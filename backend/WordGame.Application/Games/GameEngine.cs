using WordGame.Application.Categories;
using WordGame.Application.Challenges;
using WordGame.Application.Sessions;
using WordGame.Domain;

namespace WordGame.Application.Games;

// Shared backend orchestration. Transport adapters must authenticate callers before invoking player actions.
public sealed class GameEngine(IGameSessionStore sessions, SessionRetention retention, CategorySelectionService categories, ChallengeService challenges)
{
    private readonly GameCommandExecutor commands = new(sessions, retention);
    public async Task<GameEngineResult> CreateAsync(IEnumerable<Player> players, CancellationToken cancellationToken = default)
    {
        try
        {
            return GameEngineResult.Accepted(await sessions.CreateAsync(players, cancellationToken));
        }
        catch (ArgumentException)
        {
            return GameEngineResult.Rejected(GameEngineErrorCode.InvalidAction, "A game requires two to four distinct valid players.");
        }
    }

    public async Task<GameEngineResult> GetStateAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        var state = await sessions.GetAsync(gameId, cancellationToken);
        return state is null ? GameEngineResult.Missing() : GameEngineResult.Accepted(state);
    }

    // Internal query; transport adapters must verify membership before returning results.
    public async Task<GameEngineResult> GetRoundResultAsync(Guid gameId, Guid roundId, CancellationToken cancellationToken = default)
    {
        var state = await sessions.GetAsync(gameId, cancellationToken);
        return state is not null && state.Game.RoundResults.TryGetValue(roundId, out var result)
            ? GameEngineResult.Accepted(state, roundResult: result)
            : GameEngineResult.Rejected(GameEngineErrorCode.NotFound, "The completed round result was not found.");
    }

    // Initialization/starting/completion/cancellation are trusted backend operations, not arbitrary player commands.
    public Task<GameEngineResult> InitializeAsync(Guid gameId, CancellationToken cancellationToken = default) =>
        commands.UpdateAsync(gameId, session => session.BeginStarting(), cancellationToken);

    public Task<GameEngineResult> StartAsync(Guid gameId, CancellationToken cancellationToken = default) =>
        commands.UpdateAsync(gameId, session =>
        {
            GamePhaseGuard.Require(session.Status == SessionStatus.Starting, "Only an initialized game can start.");
            session.Game.StartRound(Guid.NewGuid(), TurnPolicy.FirstPlayer(session, retention.DisconnectedGrace));
            session.Activate();
            session.BeginCategorySelection(categories.Rules.Timeout);
        }, cancellationToken);

    // Selection resolves an ID through the configured provider and pins the validated answer snapshot.
    public Task<GameEngineResult> SelectCategoryAsync(
        Guid gameId, Guid playerId, string categoryId, PlayerActionContext context, CancellationToken cancellationToken = default) =>
        categories.SelectAsync(gameId, playerId, categoryId, context, cancellationToken);

    public Task<GameEngineResult> PlaceBidAsync(
        Guid gameId, Guid playerId, int wordCount, PlayerActionContext context, CancellationToken cancellationToken = default) =>
        commands.PlayerUpdateAsync(gameId, playerId, context, session =>
        {
            var round = GamePhaseGuard.BiddingRound(session, playerId);
            round.RecordBid(new Bid(playerId, wordCount));
            round.AssignTurn(TurnPolicy.NextPlayer(session, playerId, retention.DisconnectedGrace));
        }, cancellationToken);

    public Task<GameEngineResult> CallBluffAsync(Guid gameId, Guid playerId, PlayerActionContext context, CancellationToken cancellationToken = default) =>
        challenges.BeginAsync(gameId, playerId, context, cancellationToken);

    public Task<GameEngineResult> SubmitWordAsync(Guid gameId, Guid playerId, string word, PlayerActionContext context, CancellationToken cancellationToken = default) =>
        challenges.SubmitAsync(gameId, playerId, word, context, cancellationToken);

    public Task<GameEngineResult> ResolveChallengeAsync(Guid gameId, CancellationToken cancellationToken = default) =>
        challenges.ResolveAsync(gameId, cancellationToken: cancellationToken);

    public Task<GameEngineResult> NextRoundAsync(Guid gameId, CancellationToken cancellationToken = default) =>
        commands.UpdateAsync(gameId, session => AdvanceRound(session), cancellationToken);

    // Delayed workflows must name the result and revision they intend to advance.
    public Task<GameEngineResult> NextRoundAsync(Guid gameId, Guid completedRoundId, long expectedVersion, CancellationToken cancellationToken = default) =>
        completedRoundId == Guid.Empty || expectedVersion < 0
            ? Task.FromResult(GameEngineResult.Rejected(GameEngineErrorCode.InvalidAction, "A completed round ID and nonnegative revision are required."))
            : commands.UpdateAsync(gameId, session => AdvanceRound(session, completedRoundId), cancellationToken, expectedVersion);

    private void AdvanceRound(GameSession session, Guid? completedRoundId = null)
    {
        var round = GamePhaseGuard.ActiveRound(session);
        GamePhaseGuard.Require(round.Status == RoundStatus.Completed && session.Game.RoundResults.ContainsKey(round.Id),
            "The current round must have a scored result before progression.");
        GamePhaseGuard.Require(completedRoundId is null || completedRoundId == round.Id, "The completed round is no longer current.");
        session.Game.StartRound(Guid.NewGuid(), TurnPolicy.NextPlayer(session, round.StartingPlayerId, retention.DisconnectedGrace));
        session.BeginCategorySelection(categories.Rules.Timeout);
    }

    // Match/scoring policy decides whether to continue or finish; F013 does not invent win conditions.
    public Task<GameEngineResult> CompleteAsync(Guid gameId, CancellationToken cancellationToken = default) =>
        commands.UpdateAsync(gameId, session =>
        {
            _ = GamePhaseGuard.ActiveRound(session);
            session.Game.Complete();
            session.Complete();
        }, cancellationToken);

    public Task<GameEngineResult> CancelAsync(Guid gameId, CancellationToken cancellationToken = default) =>
        commands.UpdateAsync(gameId, session => session.Abandon(), cancellationToken);

    public Task<GameEngineResult> ConnectAsync(Guid gameId, Guid playerId, Guid connectionId, CancellationToken cancellationToken = default) =>
        commands.UpdateAsync(gameId, session =>
        {
            session.Connect(playerId, connectionId);
            session.ReconcileTurns(retention.DisconnectedGrace);
        }, cancellationToken);

    public Task<GameEngineResult> DisconnectAsync(Guid gameId, Guid playerId, Guid connectionId, CancellationToken cancellationToken = default) =>
        commands.UpdateAsync(gameId, session =>
        {
            session.Disconnect(playerId, connectionId);
            session.ReconcileTurns(retention.DisconnectedGrace);
        }, cancellationToken);

    public Task<GameEngineResult> LeaveAsync(Guid gameId, Guid playerId, PlayerActionContext context, CancellationToken cancellationToken = default) =>
        commands.PlayerUpdateAsync(gameId, playerId, context, session =>
        {
            session.Leave(playerId);
            session.ReconcileTurns(retention.DisconnectedGrace);
        }, cancellationToken);

}
