using WordGame.Application.Sessions;
using WordGame.Domain;

namespace WordGame.Application.Games;

// Shared backend orchestration. Transport adapters must authenticate callers before invoking player actions.
public sealed class GameEngine(IGameSessionStore sessions, TimeProvider clock)
{
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
        return state is null ? Missing() : GameEngineResult.Accepted(state);
    }

    // Initialization/starting/completion/cancellation are trusted backend operations, not arbitrary player commands.
    public Task<GameEngineResult> InitializeAsync(Guid gameId, CancellationToken cancellationToken = default) =>
        UpdateAsync(gameId, session => session.BeginStarting(), cancellationToken);

    public Task<GameEngineResult> StartAsync(Guid gameId, Guid startingPlayerId, CancellationToken cancellationToken = default) =>
        UpdateAsync(gameId, session =>
        {
            Require(session.Status == SessionStatus.Starting, "Only an initialized game can start.");
            session.Game.StartRound(Guid.NewGuid(), startingPlayerId);
            session.Activate();
        }, cancellationToken);

    // Category metadata must be resolved by trusted backend category-selection code, never copied from a client DTO.
    public Task<GameEngineResult> SelectCategoryAsync(
        Guid gameId, Guid playerId, Category category, CancellationToken cancellationToken = default) =>
        UpdateAsync(gameId, session => PlayerRound(session, playerId).SelectCategory(playerId, category), cancellationToken);

    public Task<GameEngineResult> PlaceBidAsync(
        Guid gameId, Guid playerId, int wordCount, CancellationToken cancellationToken = default) =>
        UpdateAsync(gameId, session =>
        {
            var round = PlayerRound(session, playerId);
            round.RecordBid(new Bid(playerId, wordCount));
            round.AssignTurn(session.Game.GetNextPlayerId(playerId));
        }, cancellationToken);

    // Duration is supplied by backend match policy. Client actions must not choose their own deadline.
    public Task<GameEngineResult> CallBluffAsync(
        Guid gameId, Guid playerId, TimeSpan duration, CancellationToken cancellationToken = default) =>
        UpdateAsync(gameId, session =>
        {
            var round = PlayerRound(session, playerId);
            Require(round.Status == RoundStatus.Bidding && round.HighestBid is not null, "A bluff call requires an existing bid.");
            var now = clock.GetUtcNow();
            round.BeginChallenge(new Challenge(Guid.NewGuid(), round.HighestBid!, playerId, now, now.Add(duration)));
        }, cancellationToken);

    // Word validation/submission is a separate use case. Outcome is derived from stored server-validated submissions.
    public Task<GameEngineResult> ResolveChallengeAsync(Guid gameId, CancellationToken cancellationToken = default) =>
        UpdateAsync(gameId, session =>
        {
            var round = ActiveRound(session);
            Require(round.Status == RoundStatus.Challenge && round.Challenge is not null, "There is no active challenge.");
            var challenge = round.Challenge!;
            var succeeded = challenge.AcceptedWordCount >= challenge.Bid.WordCount;
            Require(succeeded || clock.GetUtcNow() >= challenge.Deadline, "An incomplete challenge cannot end before its deadline.");
            challenge.Resolve(succeeded ? ChallengeOutcome.Succeeded : ChallengeOutcome.Failed);
            round.Complete();
        }, cancellationToken);

    public Task<GameEngineResult> NextRoundAsync(Guid gameId, Guid startingPlayerId, CancellationToken cancellationToken = default) =>
        UpdateAsync(gameId, session =>
        {
            Require(ActiveRound(session).Status == RoundStatus.Completed, "The current round must finish first.");
            session.Game.StartRound(Guid.NewGuid(), startingPlayerId);
        }, cancellationToken);

    // Match/scoring policy decides whether to continue or finish; F013 does not invent win conditions.
    public Task<GameEngineResult> CompleteAsync(Guid gameId, CancellationToken cancellationToken = default) =>
        UpdateAsync(gameId, session =>
        {
            _ = ActiveRound(session);
            session.Game.Complete();
            session.Complete();
        }, cancellationToken);

    public Task<GameEngineResult> CancelAsync(Guid gameId, CancellationToken cancellationToken = default) =>
        UpdateAsync(gameId, session => session.Abandon(), cancellationToken);

    private async Task<GameEngineResult> UpdateAsync(Guid gameId, Action<GameSession> action, CancellationToken cancellationToken)
    {
        try
        {
            var state = await sessions.UpdateAsync(gameId, session =>
            {
                try
                {
                    action(session);
                }
                catch (ArgumentException)
                {
                    throw new ActionRejected(GameEngineErrorCode.InvalidAction, "The action arguments are invalid.");
                }
                catch (InvalidOperationException exception)
                {
                    throw new ActionRejected(GameEngineErrorCode.InvalidState, exception.Message);
                }
            }, cancellationToken);
            return GameEngineResult.Accepted(state);
        }
        catch (ActionRejected rejection)
        {
            return GameEngineResult.Rejected(rejection.Code, rejection.Message);
        }
        catch (KeyNotFoundException)
        {
            return Missing();
        }
    }

    private static GameRound PlayerRound(GameSession session, Guid playerId)
    {
        var round = ActiveRound(session);
        if (!session.Game.Players.Any(player => player.Id == playerId))
        {
            throw new ActionRejected(GameEngineErrorCode.PlayerNotInGame, "The player is not in this game.");
        }
        if (round.Turn.PlayerId != playerId)
        {
            throw new ActionRejected(GameEngineErrorCode.NotYourTurn, "The player does not hold the current turn.");
        }
        return round;
    }

    private static GameRound ActiveRound(GameSession session)
    {
        Require(session.Status == SessionStatus.Active && session.Game.CurrentRound is not null, "The game is not active.");
        return session.Game.CurrentRound!;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new ActionRejected(GameEngineErrorCode.InvalidState, message);
        }
    }

    private static GameEngineResult Missing() => GameEngineResult.Rejected(GameEngineErrorCode.NotFound, "The game was not found.");

    private sealed class ActionRejected(GameEngineErrorCode code, string message) : Exception(message)
    {
        public GameEngineErrorCode Code { get; } = code;
    }
}
