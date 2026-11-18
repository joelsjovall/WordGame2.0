using WordGame.Application.Games;
using WordGame.Application.Sessions;
using WordGame.Application.Words;
using WordGame.Domain;

namespace WordGame.Infrastructure.Tests;

public sealed class ChallengeConcurrencyTests
{
    [Fact]
    public async Task ConcurrentDuplicateWordsCannotBothCountAndAliasRetryRemainsDuplicate()
    {
        var (engine, store, _, id, players) = await Game(required: 2);
        var version = (await store.GetAsync(id))!.Version;
        var results = await Task.WhenAll(new[] { "Volvo", "VOLVO" }.Select(word => Task.Run(() =>
            engine.SubmitWordAsync(id, players[0].Id, word, new(Guid.NewGuid(), version)))));
        Assert.Single(results, result => result.Succeeded);
        Assert.Equal(GameEngineErrorCode.StaleState, Assert.Single(results, result => !result.Succeeded).Error!.Code);
        var after = (await store.GetAsync(id))!;
        Assert.Equal(1, after.Game.CurrentRound!.Challenge!.AcceptedWordCount);
        var retry = await engine.SubmitWordAsync(id, players[0].Id, "volvo", new(Guid.NewGuid(), after.Version));
        Assert.True(retry.Succeeded);
        Assert.Equal(WordSubmissionStatus.Duplicate, retry.SubmissionStatus);
        Assert.Equal(1, retry.State!.Game.CurrentRound!.Challenge!.AcceptedWordCount);
        Assert.Equal(2, retry.State.Game.CurrentRound.Challenge.Submissions.Count);
    }

    [Fact]
    public async Task ConcurrentTimeoutSweepsPublishOneFailureAndRetainPartialHistory()
    {
        var (engine, store, clock, id, players) = await Game(required: 2);
        var initial = (await store.GetAsync(id))!;
        Assert.True((await engine.SubmitWordAsync(id, players[0].Id, "Volvo", new(Guid.NewGuid(), initial.Version))).Succeeded);
        var before = (await store.GetAsync(id))!;
        clock.Advance(TimeSpan.FromSeconds(30));
        var service = SessionTestData.Challenges(store, Retention());
        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(() => service.ResolveAsync(id, before.Version))));
        var accepted = Assert.Single(results, result => result.Succeeded);
        Assert.Equal(ChallengeOutcome.Failed, accepted.ChallengeResult!.Outcome);
        Assert.Equal(1, accepted.ChallengeResult.AcceptedWordCount);
        Assert.Equal(1, accepted.State!.Game.Scores[players[0].Id].Points);
        Assert.Equal(50, accepted.State.Game.Scores[players[1].Id].Points);
        Assert.Equal(before.Version + 1, (await store.GetAsync(id))!.Version);
        Assert.All(results.Where(result => !result.Succeeded), result => Assert.Equal(GameEngineErrorCode.StaleState, result.Error!.Code));
    }

    [Fact]
    public async Task SubmissionLinearizedBeforeDeadlineWinsAgainstAQueuedTimeoutExactlyOnce()
    {
        using var validator = new BlockingValidator();
        var (engine, store, clock, id, players) = await Game(required: 1, validator);
        clock.Advance(TimeSpan.FromSeconds(30) - TimeSpan.FromTicks(1));
        var before = (await store.GetAsync(id))!;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var changes = store.WatchAsync(id, timeout.Token).GetAsyncEnumerator();
        Assert.True(await changes.MoveNextAsync());
        var submission = Task.Run(() => engine.SubmitWordAsync(id, players[0].Id, "Volvo", new(Guid.NewGuid(), before.Version), timeout.Token));
        Task<GameEngineResult> expiry;
        try
        {
            await validator.Entered.Task.WaitAsync(timeout.Token);
            clock.Advance(TimeSpan.FromTicks(1));
            expiry = SessionTestData.Challenges(store, Retention()).ResolveAsync(id, before.Version, timeout.Token);
        }
        finally
        {
            validator.Release.Set();
        }
        var accepted = await submission;
        Assert.Equal(ChallengeOutcome.Succeeded, accepted.ChallengeResult!.Outcome);
        Assert.Equal(GameEngineErrorCode.StaleState, (await expiry).Error!.Code);
        var after = (await store.GetAsync(id))!;
        Assert.Equal(before.Version + 1, after.Version);
        Assert.Equal(51, after.Game.Scores[players[0].Id].Points);
        Assert.Equal(0, after.Game.Scores[players[1].Id].Points);
        var word = Assert.Single(after.Game.CurrentRound!.Challenge!.Submissions);
        Assert.Equal(after.Game.CurrentRound.Challenge.Deadline - TimeSpan.FromTicks(1), word.SubmittedAt);
        Assert.True(await changes.MoveNextAsync());
        Assert.Equal(after.Version, changes.Current.Version);
    }

    [Fact]
    public async Task ASubmissionAtDeadlineCannotBeatTimeoutResolution()
    {
        var (engine, store, clock, id, players) = await Game(required: 1);
        var before = (await store.GetAsync(id))!;
        clock.Advance(TimeSpan.FromSeconds(30));
        var submission = Task.Run(() => engine.SubmitWordAsync(id, players[0].Id, "Volvo", new(Guid.NewGuid(), before.Version)));
        var expiry = Task.Run(() => SessionTestData.Challenges(store, Retention()).ResolveAsync(id, before.Version));
        Assert.False((await submission).Succeeded);
        Assert.Equal(ChallengeOutcome.Failed, (await expiry).ChallengeResult!.Outcome);
        var after = (await store.GetAsync(id))!;
        Assert.Equal(before.Version + 1, after.Version);
        Assert.Empty(after.Game.CurrentRound!.Challenge!.Submissions);
    }

    private static async Task<(GameEngine Engine, IGameSessionStore Store, ManualSessionClock Clock, Guid Id, Player[] Players)> Game(
        int required, IWordValidationService? validator = null)
    {
        var clock = new ManualSessionClock();
        var store = SessionTestData.Store(clock);
        var engine = SessionTestData.Engine(store, validator);
        var players = SessionTestData.Players();
        var id = (await engine.CreateAsync(players)).State!.Id;
        Assert.True((await engine.InitializeAsync(id)).Succeeded);
        Assert.True((await engine.StartAsync(id)).Succeeded);
        Assert.True((await engine.SelectCategoryAsync(id, players[0].Id, "cars", new(Guid.NewGuid(), (await store.GetAsync(id))!.Version))).Succeeded);
        Assert.True((await engine.PlaceBidAsync(id, players[0].Id, required, new(Guid.NewGuid(), (await store.GetAsync(id))!.Version))).Succeeded);
        Assert.True((await engine.CallBluffAsync(id, players[1].Id, new(Guid.NewGuid(), (await store.GetAsync(id))!.Version))).Succeeded);
        return (engine, store, clock, id, players);
    }

    private static SessionRetention Retention() => new(TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(5));

    private sealed class BlockingValidator : IWordValidationService, IDisposable
    {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal ManualResetEventSlim Release { get; } = new();
        public WordValidationResult Validate(CategoryDefinition category, string? word, IReadOnlyCollection<SubmittedWord> previousSubmissions)
        {
            Entered.TrySetResult();
            if (!Release.Wait(TimeSpan.FromSeconds(5)))
            {
                throw new TimeoutException("Test did not release its validator.");
            }

            return new WordValidationService().Validate(category, word, previousSubmissions);
        }
        public void Dispose() => Release.Dispose();
    }
}
