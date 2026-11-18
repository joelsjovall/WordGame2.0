using WordGame.Application.Games;
using WordGame.Domain;

namespace WordGame.Application.Tests.Games;

public sealed class WordSubmissionTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task IssueExampleCompletesAutomaticallyWithFiveUniqueAnswers(int players)
    {
        var game = await ChallengeTestGame.Create(players);
        var words = new[] { "Stockholm", "Paris", "Berlin", "Stockholm", "Madrid", "Rome" };
        for (var index = 0; index < words.Length; index++)
        {
            game.Clock.Advance(TimeSpan.FromSeconds(1));
            var response = await game.Submit(0, words[index]);
            var state = TurnTestGame.Accepted(response);
            Assert.Equal(index == 3 ? WordSubmissionStatus.Duplicate : WordSubmissionStatus.Accepted, response.SubmissionStatus);
            Assert.Equal(game.Clock.GetUtcNow(), state.Game.CurrentRound!.Challenge!.Submissions[^1].SubmittedAt);
            if (index < words.Length - 1)
            {
                Assert.Null(response.ChallengeResult);
            }
            else
            {
                var result = Assert.IsType<WordGame.Application.Challenges.ChallengeResult>(response.ChallengeResult);
                Assert.Equal(ChallengeOutcome.Succeeded, result.Outcome);
                Assert.Equal(5, result.RequiredWordCount);
                Assert.Equal(5, result.AcceptedWordCount);
                Assert.Equal(game.Players[0].Id, result.BidderId);
                Assert.Equal(game.Players[1].Id, result.ChallengerId);
                Assert.Equal(RoundStatus.Completed, state.Game.CurrentRound.Status);
                Assert.Equal(result, (await game.Engine.GetStateAsync(game.Id)).ChallengeResult);
            }
        }
        var scores = (await game.State()).Game.Scores;
        Assert.Equal(55, scores[game.Players[0].Id].Points);
        Assert.All(scores.Values.Where(score => score.PlayerId != game.Players[0].Id), score => Assert.Equal(0, score.Points));
    }

    [Fact]
    public async Task InvalidWordsAndAliasesAreTrackedWithoutDoubleCountingCanonicalAnswers()
    {
        var game = await ChallengeTestGame.Create();
        Assert.Equal(WordSubmissionStatus.Rejected, (await game.Submit(0, "Banana")).SubmissionStatus);
        Assert.Equal(WordSubmissionStatus.Accepted, (await game.Submit(0, " Copenhagen ")).SubmissionStatus);
        Assert.Equal(WordSubmissionStatus.Duplicate, (await game.Submit(0, "KÖPENHAMN")).SubmissionStatus);
        Assert.Equal(WordSubmissionStatus.Duplicate, (await game.Submit(0, "copenhagen")).SubmissionStatus);
        var challenge = (await game.State()).Game.CurrentRound!.Challenge!;
        Assert.Equal(1, challenge.AcceptedWordCount);
        Assert.Equal(4, challenge.Submissions.Count);
        Assert.Equal(ChallengeOutcome.Pending, challenge.Outcome);
        Assert.Equal(new[] { WordSubmissionStatus.Rejected, WordSubmissionStatus.Accepted, WordSubmissionStatus.Duplicate, WordSubmissionStatus.Duplicate },
            challenge.Submissions.Select(word => word.Status));
    }

    [Fact]
    public async Task OnlyTheChallengedPlayerCanSubmitAndBlankRequestsHaveNoHistoryEntry()
    {
        var game = await ChallengeTestGame.Create(3);
        var before = await game.State();
        foreach (var player in game.Players.Skip(1))
        {
            Assert.Equal(GameEngineErrorCode.NotYourTurn,
                (await game.Engine.SubmitWordAsync(game.Id, player.Id, "Paris", await game.Context())).Error!.Code);
        }

        Assert.Equal(GameEngineErrorCode.PlayerNotInGame,
            (await game.Engine.SubmitWordAsync(game.Id, Guid.NewGuid(), "Paris", await game.Context())).Error!.Code);
        foreach (var word in new[] { "", " \t ", null! })
        {
            Assert.Equal(GameEngineErrorCode.InvalidAction, (await game.Submit(0, word)).Error!.Code);
        }

        Assert.Equal(before.Version, (await game.State()).Version);
        Assert.Empty((await game.State()).Game.CurrentRound!.Challenge!.Submissions);
    }

    [Fact]
    public async Task NetworkRetriesCannotAddHistoryOrCountTheSameActionTwice()
    {
        var game = await ChallengeTestGame.Create();
        var context = await game.Context();
        TurnTestGame.Accepted(await game.Submit(0, "Paris", context));
        var version = (await game.State()).Version;
        Assert.Equal(GameEngineErrorCode.StaleState, (await game.Submit(0, "Paris", context)).Error!.Code);
        Assert.Equal(GameEngineErrorCode.DuplicateAction, (await game.Submit(0, "Rome", await game.Context(context.ActionId))).Error!.Code);
        Assert.Equal(version, (await game.State()).Version);
        Assert.Single((await game.State()).Game.CurrentRound!.Challenge!.Submissions);
    }

    [Fact]
    public async Task CompletionAndFurtherSubmissionsCannotBeProcessedTwice()
    {
        var game = await ChallengeTestGame.Create(required: 1);
        var completed = await game.Submit(0, "Paris");
        Assert.Equal(ChallengeOutcome.Succeeded, completed.ChallengeResult!.Outcome);
        var before = await game.State();
        Assert.Equal(GameEngineErrorCode.InvalidState, (await game.Submit(0, "Rome")).Error!.Code);
        Assert.Equal(GameEngineErrorCode.InvalidState, (await game.Engine.ResolveChallengeAsync(game.Id)).Error!.Code);
        var after = await game.State();
        Assert.Equal(before.Version, after.Version);
        Assert.Single(after.Game.CurrentRound!.Challenge!.Submissions);
        Assert.Equal(completed.ChallengeResult, (await game.Engine.GetStateAsync(game.Id)).ChallengeResult);
    }

    [Fact]
    public async Task SubmissionUsesThePinnedDefinitionWithoutReadingTheProviderAgain()
    {
        var provider = new TestCategoryProvider(ChallengeTestGame.Capitals());
        var game = await ChallengeTestGame.Create(required: 1, provider: provider);
        provider.Definitions["capitals"] = new CategoryDefinition(new Category("capitals", "Changed", CategoryDifficulty.Hard), [new AcceptedAnswer("Different")]);
        provider.LoadOverride = (_, _) => throw new IOException("Provider must not be read during submissions.");
        var result = await game.Submit(0, "Paris");
        Assert.True(result.Succeeded);
        Assert.Equal(WordSubmissionStatus.Accepted, result.SubmissionStatus);
        Assert.Equal(ChallengeOutcome.Succeeded, result.ChallengeResult!.Outcome);
        Assert.Equal(CategoryDifficulty.Easy, result.State!.SelectedCategory!.Definition.Category.Difficulty);
    }

    [Fact]
    public async Task SubmissionRequiresChallengePhaseAndCancellationPropagates()
    {
        var game = await TurnTestGame.Create();
        Assert.Equal(GameEngineErrorCode.InvalidState, (await game.Submit(0, "Paris")).Error!.Code);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await game.Engine.SubmitWordAsync(game.Id, game.Players[0].Id, "Paris", await game.Context(), cancelled.Token));
    }
}
