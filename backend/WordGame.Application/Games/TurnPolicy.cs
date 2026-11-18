using WordGame.Application.Sessions;
using WordGame.Domain;

namespace WordGame.Application.Games;

internal static class TurnPolicy
{
    internal static Guid FirstPlayer(GameSession session, TimeSpan grace)
    {
        RequireTwoPlayers(session, grace);
        return session.Game.Players.First(player => session.CanTakeTurn(player.Id, grace)).Id;
    }

    internal static Guid NextPlayer(GameSession session, Guid after, TimeSpan grace)
    {
        RequireTwoPlayers(session, grace);
        var next = session.Game.GetNextPlayerId(after);
        while (!session.CanTakeTurn(next, grace))
        {
            next = session.Game.GetNextPlayerId(next);
        }

        return next;
    }

    internal static bool Reconcile(GameSession session, TimeSpan grace)
    {
        if (session.Status is not (SessionStatus.Starting or SessionStatus.Active))
        {
            return false;
        }

        var round = session.Game.CurrentRound;
        var essentialPlayerMissing = round?.Status is RoundStatus.Bidding or RoundStatus.Challenge &&
            (round.HighestBid is { } bid && !session.CanTakeTurn(bid.PlayerId, grace) ||
             round.Challenge is { } challenge && !session.CanTakeTurn(challenge.ChallengerId, grace));
        if (EligibleCount(session, grace) < 2 || essentialPlayerMissing)
        {
            session.Abandon();
            return true;
        }
        if (round?.Status is RoundStatus.CategorySelection or RoundStatus.Bidding && !session.CanTakeTurn(round.Turn.PlayerId, grace))
        {
            round.AssignTurn(NextPlayer(session, round.Turn.PlayerId, grace));
            return true;
        }
        return false;
    }

    private static int EligibleCount(GameSession session, TimeSpan grace) =>
        session.Game.Players.Count(player => session.CanTakeTurn(player.Id, grace));

    private static void RequireTwoPlayers(GameSession session, TimeSpan grace)
    {
        if (EligibleCount(session, grace) < 2)
        {
            throw new InvalidOperationException("At least two eligible players are required.");
        }
    }
}
