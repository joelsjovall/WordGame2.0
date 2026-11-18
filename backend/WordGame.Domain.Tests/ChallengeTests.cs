namespace WordGame.Domain.Tests;

public sealed class ChallengeTests
{
    [Fact]
    public void ChallengeRejectsSelfChallengeAndInvalidTimeWindow()
    {
        var bid = new Bid(Guid.NewGuid(), 2);
        var start = DomainTestData.StartedAt;
        Assert.Throws<ArgumentException>(() => new Challenge(Guid.NewGuid(), bid, bid.PlayerId, start, start.AddSeconds(30)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Challenge(Guid.NewGuid(), bid, Guid.NewGuid(), start, start));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Challenge(Guid.NewGuid(), bid, Guid.NewGuid(), start, start.AddSeconds(-1)));
    }

    [Fact]
    public void SubmissionsAreRestrictedToTheBidderAndTimeWindow()
    {
        var challenge = CreateChallenge();
        var bidder = challenge.Bid.PlayerId;
        Assert.Throws<InvalidOperationException>(() => challenge.RecordSubmission(
            new SubmittedWord(Guid.NewGuid(), "word", challenge.StartedAt, WordSubmissionStatus.Rejected)));
        Assert.Throws<InvalidOperationException>(() => challenge.RecordSubmission(
            new SubmittedWord(bidder, "word", challenge.StartedAt.AddTicks(-1), WordSubmissionStatus.Rejected)));
        Assert.Throws<InvalidOperationException>(() => challenge.RecordSubmission(
            new SubmittedWord(bidder, "word", challenge.Deadline, WordSubmissionStatus.Rejected)));
        challenge.RecordSubmission(new SubmittedWord(bidder, "word", challenge.StartedAt, WordSubmissionStatus.Rejected));
        challenge.RecordSubmission(new SubmittedWord(bidder, "word", challenge.Deadline.AddTicks(-1), WordSubmissionStatus.Rejected));
        Assert.Equal(2, challenge.Submissions.Count);
    }

    [Fact]
    public void RejectedAndDuplicateSubmissionsDoNotIncreaseAcceptedCount()
    {
        var challenge = CreateChallenge();
        challenge.RecordSubmission(DomainTestData.AcceptedWord(challenge));
        challenge.RecordSubmission(new SubmittedWord(challenge.Bid.PlayerId, "wrong",
            challenge.StartedAt.AddSeconds(2), WordSubmissionStatus.Rejected));
        challenge.RecordSubmission(new SubmittedWord(challenge.Bid.PlayerId, "Stockholm",
            challenge.StartedAt.AddSeconds(3), WordSubmissionStatus.Duplicate));

        Assert.Equal(3, challenge.Submissions.Count);
        Assert.Equal(1, challenge.AcceptedWordCount);
        Assert.False(challenge.HasMetBid);
        Assert.Throws<NotSupportedException>(() => ((IList<SubmittedWord>)challenge.Submissions).Clear());
    }

    [Fact]
    public void CanonicalAnswerIdentityPreventsAliasesFromCountingTwice()
    {
        var challenge = CreateChallenge();
        challenge.RecordSubmission(DomainTestData.AcceptedWord(challenge, "answer:1"));
        Assert.Throws<InvalidOperationException>(() => challenge.RecordSubmission(
            new SubmittedWord(challenge.Bid.PlayerId, "alternative spelling",
                challenge.StartedAt.AddSeconds(2), WordSubmissionStatus.Accepted, "answer:1")));
        Assert.Equal(1, challenge.AcceptedWordCount);
    }

    [Fact]
    public void SuccessRequiresEnoughAcceptedWordsAndIsTerminal()
    {
        var challenge = CreateChallenge();
        Assert.False(challenge.HasMetBid);
        Assert.Throws<InvalidOperationException>(() => challenge.Resolve(ChallengeOutcome.Succeeded));
        challenge.RecordSubmission(DomainTestData.AcceptedWord(challenge, "answer:1"));
        challenge.RecordSubmission(DomainTestData.AcceptedWord(challenge, "answer:2"));
        Assert.True(challenge.HasMetBid);
        Assert.Throws<InvalidOperationException>(() => challenge.Resolve(ChallengeOutcome.Failed));
        challenge.Resolve(ChallengeOutcome.Succeeded);

        Assert.Equal(ChallengeOutcome.Succeeded, challenge.Outcome);
        Assert.Throws<InvalidOperationException>(() => challenge.Resolve(ChallengeOutcome.Succeeded));
        Assert.Throws<InvalidOperationException>(() => challenge.RecordSubmission(DomainTestData.AcceptedWord(challenge, "answer:3")));
    }

    [Fact]
    public void FailedOutcomeIsTerminalAndInvalidOutcomesAreRejected()
    {
        var challenge = CreateChallenge();
        Assert.Throws<ArgumentOutOfRangeException>(() => challenge.Resolve((ChallengeOutcome)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => challenge.Resolve(ChallengeOutcome.Pending));
        Assert.Throws<ArgumentOutOfRangeException>(() => challenge.Resolve(ChallengeOutcome.Cancelled));
        challenge.Resolve(ChallengeOutcome.Failed);
        Assert.Equal(ChallengeOutcome.Failed, challenge.Outcome);
        Assert.Throws<InvalidOperationException>(() => challenge.Resolve(ChallengeOutcome.Failed));
    }

    private static Challenge CreateChallenge() =>
        new(Guid.NewGuid(), new Bid(Guid.NewGuid(), 2), Guid.NewGuid(),
            DomainTestData.StartedAt, DomainTestData.StartedAt.AddSeconds(30));
}
