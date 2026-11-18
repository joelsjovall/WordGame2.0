namespace WordGame.Domain;

public sealed class ScoringRules
{
    public ScoringRules(int outcomeBonus = 50)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(outcomeBonus);
        OutcomeBonus = outcomeBonus;
    }

    public int OutcomeBonus { get; }

    // Accept only authoritative, completed rounds; the game owns the award replay guard.
    public IReadOnlyList<PlayerScore> Calculate(GameRound round, int pointsPerWord)
    {
        ArgumentNullException.ThrowIfNull(round);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pointsPerWord);
        var challenge = round.Challenge;
        if (round.Status != RoundStatus.Completed || round.Category is null ||
            challenge is null || challenge.Outcome is not (ChallengeOutcome.Succeeded or ChallengeOutcome.Failed))
        {
            throw new InvalidOperationException("Scoring requires a completed challenge round.");
        }

        var wordPoints = checked(challenge.AcceptedWordCount * pointsPerWord);
        return challenge.Outcome == ChallengeOutcome.Succeeded
            ? [new PlayerScore(challenge.Bid.PlayerId, checked(wordPoints + OutcomeBonus))]
            : [new PlayerScore(challenge.Bid.PlayerId, wordPoints), new PlayerScore(challenge.ChallengerId, OutcomeBonus)];
    }
}
