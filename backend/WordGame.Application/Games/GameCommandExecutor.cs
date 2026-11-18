using WordGame.Application.Sessions;

namespace WordGame.Application.Games;

// Shared transaction and player-action guards for engine use cases.
internal sealed class GameCommandExecutor(IGameSessionStore sessions, SessionRetention retention)
{
    internal Task<GameEngineResult> PlayerUpdateAsync(Guid gameId, Guid playerId, PlayerActionContext context,
        Action<GameSession> action, CancellationToken cancellationToken)
    {
        if (context is null || context.ActionId == Guid.Empty || context.ExpectedVersion < 0)
        {
            return Task.FromResult(GameEngineResult.Rejected(GameEngineErrorCode.InvalidAction, "An action ID and nonnegative revision are required."));
        }

        return UpdateAsync(gameId, session =>
        {
            if (session.Status is SessionStatus.Completed or SessionStatus.Abandoned)
            {
                throw new GameActionRejectedException(GameEngineErrorCode.InvalidState, "The game has ended.");
            }

            if (!session.Game.Players.Any(player => player.Id == playerId))
            {
                throw new GameActionRejectedException(GameEngineErrorCode.PlayerNotInGame, "The player is not in this game.");
            }

            if (session.HasProcessedAction(context.ActionId))
            {
                throw new GameActionRejectedException(GameEngineErrorCode.DuplicateAction, "This action has already been processed.");
            }

            if (!session.CanTakeTurn(playerId, retention.DisconnectedGrace))
            {
                throw new GameActionRejectedException(GameEngineErrorCode.PlayerUnavailable, "The player is not eligible to act.");
            }

            action(session);
            session.RecordAction(context.ActionId);
        }, cancellationToken, context.ExpectedVersion);
    }

    internal async Task<GameEngineResult> UpdateAsync(Guid gameId, Action<GameSession> action, CancellationToken cancellationToken, long? expectedVersion = null)
    {
        try
        {
            void Apply(GameSession session)
            {
                try
                {
                    action(session);
                }
                catch (ArgumentException)
                {
                    throw new GameActionRejectedException(GameEngineErrorCode.InvalidAction, "The action arguments are invalid.");
                }
                catch (InvalidOperationException exception)
                {
                    throw new GameActionRejectedException(GameEngineErrorCode.InvalidState, exception.Message);
                }
                catch (OverflowException)
                {
                    throw new GameActionRejectedException(GameEngineErrorCode.InvalidState, "The action exceeds the supported numeric range.");
                }
            }
            var state = expectedVersion is { } version
                ? await sessions.UpdateAsync(gameId, version, Apply, cancellationToken)
                : await sessions.UpdateAsync(gameId, Apply, cancellationToken);
            return GameEngineResult.Accepted(state);
        }
        catch (GameActionRejectedException rejection)
        {
            return GameEngineResult.Rejected(rejection.Code, rejection.Message);
        }
        catch (KeyNotFoundException)
        {
            return GameEngineResult.Missing();
        }
        catch (SessionVersionConflictException)
        {
            return GameEngineResult.Rejected(GameEngineErrorCode.StaleState, "The game state has changed; retrieve a fresh snapshot.");
        }
    }

}

internal sealed class GameActionRejectedException(GameEngineErrorCode code, string message) : Exception(message)
{
    public GameEngineErrorCode Code { get; } = code;
}
