namespace WordGame.Domain;

public sealed class Challenge
{
    private readonly List<SubmittedWord> submissions = [];
    private bool attachedToRound;

    public Challenge(Guid id, Bid bid, Guid challengerId, DateTimeOffset startedAt, DateTimeOffset deadline)
    {
        ArgumentNullException.ThrowIfNull(bid);
        Id = DomainGuard.Id(id, nameof(id));
        ChallengerId = DomainGuard.Id(challengerId, nameof(challengerId));
        if (challengerId == bid.PlayerId)
        {
            throw new ArgumentException("A player cannot challenge their own bid.", nameof(challengerId));
        }

        if (deadline <= startedAt)
        {
            throw new ArgumentOutOfRangeException(nameof(deadline), "The deadline must follow the start.");
        }

        Bid = bid;
        StartedAt = startedAt;
        Deadline = deadline;
        Submissions = submissions.AsReadOnly();
    }

    public Guid Id { get; }
    public Bid Bid { get; }
    public Guid ChallengerId { get; }
    public DateTimeOffset StartedAt { get; }
    public DateTimeOffset Deadline { get; }
    public IReadOnlyList<SubmittedWord> Submissions { get; }
    public ChallengeOutcome Outcome { get; private set; }
    public int AcceptedWordCount => submissions.Count(word => word.Status == WordSubmissionStatus.Accepted);
    public bool HasMetBid => AcceptedWordCount >= Bid.WordCount;

    internal Challenge Copy()
    {
        var copy = new Challenge(Id, Bid, ChallengerId, StartedAt, Deadline)
        {
            Outcome = Outcome,
            attachedToRound = attachedToRound
        };
        copy.submissions.AddRange(submissions);
        return copy;
    }

    // Validation and timestamps are supplied by trusted backend code, never a client DTO.
    public void RecordSubmission(SubmittedWord submission)
    {
        ArgumentNullException.ThrowIfNull(submission);
        DomainGuard.State(Outcome == ChallengeOutcome.Pending, "The challenge has already ended.");
        DomainGuard.State(submission.PlayerId == Bid.PlayerId, "Only the challenged player can submit words.");
        DomainGuard.State(submission.SubmittedAt >= StartedAt && submission.SubmittedAt < Deadline,
            "The submission must be inside the challenge time window.");

        if (submission.Status == WordSubmissionStatus.Accepted)
        {
            DomainGuard.State(!submissions.Any(word =>
                word.Status == WordSubmissionStatus.Accepted &&
                StringComparer.Ordinal.Equals(word.CanonicalAnswerId, submission.CanonicalAnswerId)),
                "An accepted answer cannot be counted twice.");
        }

        submissions.Add(submission);
    }

    // The engine decides when to resolve; this model never reads a clock or runs a timer.
    public void Resolve(ChallengeOutcome outcome)
    {
        if (outcome is not (ChallengeOutcome.Succeeded or ChallengeOutcome.Failed))
        {
            throw new ArgumentOutOfRangeException(nameof(outcome));
        }

        DomainGuard.State(Outcome == ChallengeOutcome.Pending, "The challenge has already ended.");
        DomainGuard.State(outcome == (HasMetBid ? ChallengeOutcome.Succeeded : ChallengeOutcome.Failed),
            "The outcome must agree with the accepted word count.");
        Outcome = outcome;
    }

    internal void AttachToRound()
    {
        DomainGuard.State(!attachedToRound, "A challenge cannot be shared by multiple rounds.");
        attachedToRound = true;
    }

    internal void Cancel()
    {
        if (Outcome == ChallengeOutcome.Pending)
        {
            Outcome = ChallengeOutcome.Cancelled;
        }
    }
}
