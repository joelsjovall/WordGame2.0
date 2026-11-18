using WordGame.Application.Games;
using WordGame.Application.Words;
using WordGame.Domain;

namespace WordGame.Infrastructure.Tests;

public sealed class GameplayFailureTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task CallbackFailuresAndMidValidationCancellationDoNotConsumeTheAction(int failure)
    {
        using var cancellation = new CancellationTokenSource();
        var validator = new InterruptingValidator(() =>
        {
            if (failure == 0)
            {
                throw new IOException("Unexpected validator failure.");
            }

            if (failure == 1)
            {
                throw new KeyNotFoundException("Unexpected validator lookup failure.");
            }

            cancellation.Cancel();
        });
        var store = SessionTestData.Store(new ManualSessionClock());
        var engine = SessionTestData.Engine(store, validator);
        var players = SessionTestData.Players();
        var id = (await engine.CreateAsync(players, MatchSettings.Party(1))).State!.Id;
        Assert.True((await engine.InitializeAsync(id)).Succeeded);
        Assert.True((await engine.StartAsync(id)).Succeeded);
        async Task<PlayerActionContext> Context() => new(Guid.NewGuid(), (await store.GetAsync(id))!.Version);
        Assert.True((await engine.SelectCategoryAsync(id, players[0].Id, "cars", await Context())).Succeeded);
        Assert.True((await engine.PlaceBidAsync(id, players[0].Id, 1, await Context())).Succeeded);
        Assert.True((await engine.CallBluffAsync(id, players[1].Id, await Context())).Succeeded);
        var before = (await store.GetAsync(id))!;
        var action = await Context();
        await using var notifications = store.WatchAsync(id).GetAsyncEnumerator();
        Assert.True(await notifications.MoveNextAsync());
        if (failure == 1)
        {
            await Assert.ThrowsAsync<KeyNotFoundException>(() => engine.SubmitWordAsync(id, players[0].Id, "Volvo", action));
        }
        else if (failure == 0)
        {
            await Assert.ThrowsAsync<IOException>(() => engine.SubmitWordAsync(id, players[0].Id, "Volvo", action));
        }
        else
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                engine.SubmitWordAsync(id, players[0].Id, "Volvo", action, cancellation.Token));
        }
        var after = (await store.GetAsync(id))!;
        Assert.Equal(before.Version, after.Version);
        Assert.Equal(before.UpdatedAt, after.UpdatedAt);
        Assert.Equal(ChallengeOutcome.Pending, after.Game.CurrentRound!.Challenge!.Outcome);
        Assert.Empty(after.Game.CurrentRound.Challenge.Submissions);
        Assert.Empty(after.Game.RoundResults);
        Assert.Null(after.Game.Result);
        Assert.All(after.Game.Scores.Values, score => Assert.Equal(0, score.Points));
        validator.Interrupt = null;
        var retry = await engine.SubmitWordAsync(id, players[0].Id, "Volvo", action);
        Assert.True(retry.Succeeded);
        Assert.Equal(before.Version + 1, retry.State!.Version);
        Assert.Equal(players[0].Id, Assert.Single(retry.MatchResult!.WinnerIds));
        Assert.True(await notifications.MoveNextAsync());
        Assert.Equal(retry.State.Version, notifications.Current.Version);
        Assert.Equal(GameEngineErrorCode.NotFound,
            (await engine.SubmitWordAsync(Guid.NewGuid(), players[0].Id, "Volvo", new(Guid.NewGuid(), 0))).Error!.Code);
    }

    private sealed class InterruptingValidator(Action interrupt) : IWordValidationService
    {
        internal Action? Interrupt { get; set; } = interrupt;
        public WordValidationResult Validate(CategoryDefinition category, string? word, IReadOnlyCollection<SubmittedWord> previousSubmissions)
        {
            Interrupt?.Invoke();
            return new WordValidationService().Validate(category, word, previousSubmissions);
        }
    }
}
