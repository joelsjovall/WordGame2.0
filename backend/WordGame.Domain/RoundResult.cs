namespace WordGame.Domain;

public sealed record RoundPlayerScore(Guid PlayerId, int PointsAwarded, int TotalPoints);

// Frozen at scoring time: later rounds and presence changes cannot rewrite a result.
public sealed class RoundResult
{
    internal RoundResult(GameRound round, IReadOnlyList<RoundPlayerScore> scores)
    {
        var challenge = round.Challenge ?? throw new InvalidOperationException("A round result requires a challenge.");
        DomainGuard.State(round.Status == RoundStatus.Completed && round.Category is not null &&
            challenge.Outcome is ChallengeOutcome.Succeeded or ChallengeOutcome.Failed, "Only completed challenges produce round results.");
        RoundId = round.Id;
        RoundNumber = round.Number;
        Category = round.Category!;
        ChallengeId = challenge.Id;
        BidderId = challenge.Bid.PlayerId;
        CallerId = challenge.ChallengerId;
        RequiredWordCount = challenge.Bid.WordCount;
        AcceptedWordCount = challenge.AcceptedWordCount;
        Outcome = challenge.Outcome;
        WinnerId = Outcome == ChallengeOutcome.Succeeded ? BidderId : CallerId;
        Submissions = Array.AsReadOnly(challenge.Submissions.ToArray());
        Scores = Array.AsReadOnly(scores.ToArray());
    }

    public Guid RoundId { get; }
    public int RoundNumber { get; }
    public Category Category { get; }
    public Guid ChallengeId { get; }
    public Guid BidderId { get; }
    public Guid CallerId { get; }
    public Guid WinnerId { get; }
    public int RequiredWordCount { get; }
    public int AcceptedWordCount { get; }
    public ChallengeOutcome Outcome { get; }
    public IReadOnlyList<SubmittedWord> Submissions { get; }
    public IReadOnlyList<RoundPlayerScore> Scores { get; }
}
