using WordGame.Domain;

namespace WordGame.Application.Challenges;

// Internal application result; API adapters project explicit public contracts.
public sealed record ChallengeResult(Guid RoundId, Guid ChallengeId, Guid BidderId, Guid ChallengerId,
    int RequiredWordCount, int AcceptedWordCount, ChallengeOutcome Outcome, DateTimeOffset StartedAt, DateTimeOffset Deadline)
{
    internal static ChallengeResult From(GameRound round)
    {
        var challenge = round.Challenge!;
        return new(round.Id, challenge.Id, challenge.Bid.PlayerId, challenge.ChallengerId,
            challenge.Bid.WordCount, challenge.AcceptedWordCount, challenge.Outcome, challenge.StartedAt, challenge.Deadline);
    }
}
