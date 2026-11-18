using WordGame.Application.Challenges;
using WordGame.Application.Games;
using WordGame.Domain;

namespace WordGame.Application.Tests.Games;

public sealed class ChallengeTimingTests
{
    [Theory]
    [InlineData(7)]
    [InlineData(30)]
    public async Task ConfiguredDurationAndServerClockDetermineTheDeadline(int seconds)
    {
        var game = await ChallengeTestGame.Create(duration: TimeSpan.FromSeconds(seconds));
        var challenge = (await game.State()).Game.CurrentRound!.Challenge!;
        Assert.Equal(game.Clock.GetUtcNow(), challenge.StartedAt);
        Assert.Equal(challenge.StartedAt.AddSeconds(seconds), challenge.Deadline);
        Assert.Equal(5, challenge.Bid.WordCount);
        Assert.Equal(ChallengeOutcome.Pending, challenge.Outcome);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonpositiveDurationsAreRejected(int seconds) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new ChallengeRules(TimeSpan.FromSeconds(seconds)));

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public async Task SubmissionWindowIsHalfOpenAndLateRequestsDoNotMutateState(int ticksFromDeadline)
    {
        var game = await ChallengeTestGame.Create(required: 1);
        var before = await game.State();
        game.Clock.Advance(TurnTestGame.Duration + TimeSpan.FromTicks(ticksFromDeadline));
        var submitted = await game.Submit(0, "Paris");
        if (ticksFromDeadline < 0)
        {
            Assert.True(submitted.Succeeded);
            Assert.Equal(ChallengeOutcome.Succeeded, submitted.ChallengeResult!.Outcome);
        }
        else
        {
            Assert.Equal(GameEngineErrorCode.ChallengeExpired, submitted.Error!.Code);
            Assert.Equal(before.Version, (await game.State()).Version);
            Assert.Empty((await game.State()).Game.CurrentRound!.Challenge!.Submissions);
            var timeout = await game.Engine.ResolveChallengeAsync(game.Id);
            Assert.Equal(ChallengeOutcome.Failed, timeout.ChallengeResult!.Outcome);
            Assert.Equal(0, timeout.ChallengeResult.AcceptedWordCount);
            Assert.Equal(RoundStatus.Completed, timeout.State!.Game.CurrentRound!.Status);
            Assert.Equal(GameEngineErrorCode.InvalidState, (await game.Engine.ResolveChallengeAsync(game.Id)).Error!.Code);
            Assert.Equal(timeout.State.Version, (await game.State()).Version);
        }
    }

    [Fact]
    public async Task TimeoutRetainsPartialAcceptedRejectedAndDuplicateHistory()
    {
        var game = await ChallengeTestGame.Create();
        await game.Submit(0, "Paris");
        await game.Submit(0, "paris");
        await game.Submit(0, "Unknown");
        var before = await game.State();
        var early = await game.Challenges.ResolveAsync(game.Id, before.Version);
        Assert.Equal(GameEngineErrorCode.InvalidState, early.Error!.Code);
        Assert.Equal(before.Version, (await game.State()).Version);
        game.Clock.Advance(TurnTestGame.Duration);
        var result = await game.Challenges.ResolveAsync(game.Id, before.Version);
        Assert.Equal(ChallengeOutcome.Failed, result.ChallengeResult!.Outcome);
        Assert.Equal(1, result.ChallengeResult.AcceptedWordCount);
        Assert.Equal(3, result.State!.Game.CurrentRound!.Challenge!.Submissions.Count);
        Assert.Equal(5, result.ChallengeResult.RequiredWordCount);
    }
}
