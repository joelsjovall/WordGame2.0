namespace WordGame.Domain.Tests;

internal static class DomainTestData
{
    internal static readonly DateTimeOffset StartedAt = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);

    internal static Game CreateGame(int playerCount = 2) =>
        new(Guid.NewGuid(), Enumerable.Range(1, playerCount)
            .Select(number => new Player(Guid.NewGuid(), $"Player {number}")));

    internal static GameRound StartBidding(Game game)
    {
        var round = game.StartRound(Guid.NewGuid(), game.Players[0].Id);
        round.SelectCategory(game.Players[0].Id, new Category("capitals", "Capitals", CategoryDifficulty.Medium));
        round.RecordBid(new Bid(game.Players[0].Id, 2));
        round.AssignTurn(game.Players[1].Id);
        return round;
    }

    internal static Challenge BeginChallenge(Game game, GameRound round)
    {
        var challenge = new Challenge(
            Guid.NewGuid(), round.HighestBid!, game.Players[1].Id, StartedAt, StartedAt.AddSeconds(30));
        round.BeginChallenge(challenge);
        return challenge;
    }

    internal static GameRound CompleteRound(Game game)
    {
        var round = StartBidding(game);
        var challenge = BeginChallenge(game, round);
        challenge.Resolve(ChallengeOutcome.Failed);
        round.Complete();
        return round;
    }

    internal static SubmittedWord AcceptedWord(Challenge challenge, string answerId = "stockholm") =>
        new(challenge.Bid.PlayerId, answerId, StartedAt.AddSeconds(1), WordSubmissionStatus.Accepted, answerId);
}
