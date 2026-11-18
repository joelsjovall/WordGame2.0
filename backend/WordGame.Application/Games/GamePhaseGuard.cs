using WordGame.Application.Sessions;
using WordGame.Domain;

namespace WordGame.Application.Games;

internal static class GamePhaseGuard
{
    internal static GameRound ActiveRound(GameSession session)
    {
        Require(session.Status == SessionStatus.Active && session.Game.CurrentRound is not null, "The game is not active.");
        return session.Game.CurrentRound!;
    }

    internal static GameRound BiddingRound(GameSession session, Guid playerId)
    {
        var round = ActiveRound(session);
        Require(round.Status == RoundStatus.Bidding && session.SelectedCategory?.RoundId == round.Id,
            "Bidding requires a provider-validated category selected for this round.");
        if (round.Turn.PlayerId != playerId)
        {
            throw new GameActionRejectedException(GameEngineErrorCode.NotYourTurn, "The player does not hold the current turn.");
        }

        return round;
    }

    internal static GameRound ChallengeRound(GameSession session)
    {
        var round = ActiveRound(session);
        Require(round.Status == RoundStatus.Challenge && round.Challenge?.Outcome == ChallengeOutcome.Pending,
            "There is no pending challenge.");
        return round;
    }

    internal static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new GameActionRejectedException(GameEngineErrorCode.InvalidState, message);
        }
    }
}
