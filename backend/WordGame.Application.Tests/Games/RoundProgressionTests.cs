using WordGame.Application.Categories;
using WordGame.Application.Games;
using WordGame.Domain;

namespace WordGame.Application.Tests.Games;

public sealed class RoundProgressionTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task MultipleRoundsRetainResultsAndMatchStateWhileResettingRoundData(int players)
    {
        var timeout = TimeSpan.FromSeconds(45);
        var game = await TurnTestGame.Create(players, new TestCategoryProvider(ChallengeTestGame.Capitals()), new CategorySelectionRules(timeout));
        var results = new List<RoundResult>();
        var expectedTotals = game.Players.ToDictionary(player => player.Id, _ => 0);
        for (var index = 0; index < players * 2; index++)
        {
            var starter = index % players;
            var caller = (starter + 1) % players;
            var start = await game.State();
            Assert.Null(start.SelectedCategory);
            Assert.Null(start.Game.CurrentRound!.Category);
            Assert.Null(start.Game.CurrentRound.HighestBid);
            Assert.Null(start.Game.CurrentRound.Challenge);
            Assert.Equal(game.Clock.GetUtcNow() + timeout, start.CategorySelectionDeadline);
            Assert.Equal(game.Players[starter].Id, start.Game.CurrentRound.StartingPlayerId);
            Assert.Equal(1, start.Game.CurrentRound.Turn.Number);
            Assert.Null((await game.Engine.GetStateAsync(game.Id)).RoundResult);
            TurnTestGame.Accepted(await game.Engine.SelectCategoryAsync(game.Id, game.Players[starter].Id, "capitals", await game.Context()));
            await game.Bid(starter, 2);
            await game.Bluff(caller);
            TurnTestGame.Accepted(await game.Submit(starter, "Paris"));
            TurnTestGame.Accepted(await game.Submit(starter, "paris"));
            TurnTestGame.Accepted(await game.Submit(starter, "Unknown"));
            var success = index % 2 == 0;
            GameEngineResult completion;
            if (success)
            {
                completion = await game.Submit(starter, "Berlin");
            }
            else
            {
                game.Clock.Advance(TurnTestGame.Duration);
                completion = await game.Engine.ResolveChallengeAsync(game.Id);
            }
            var completed = TurnTestGame.Accepted(completion);
            var result = Assert.IsType<RoundResult>(completion.RoundResult);
            if (results.Count > 0)
            {
                Assert.Equal(GameEngineErrorCode.InvalidState,
                    (await game.Engine.NextRoundAsync(game.Id, results[0].RoundId, completed.Version)).Error!.Code);
            }
            results.Add(result);
            Assert.Equal(index + 1, result.RoundNumber);
            Assert.Equal(game.Players[success ? starter : caller].Id, result.WinnerId);
            Assert.Equal(success ? ChallengeOutcome.Succeeded : ChallengeOutcome.Failed, result.Outcome);
            Assert.Equal(success ? 2 : 1, result.AcceptedWordCount);
            Assert.Equal(success ? 4 : 3, result.Submissions.Count);
            Assert.Equal(WordSubmissionStatus.Duplicate, result.Submissions[1].Status);
            Assert.Equal(WordSubmissionStatus.Rejected, result.Submissions[2].Status);
            expectedTotals[game.Players[starter].Id] += success ? 52 : 1;
            if (!success)
            {
                expectedTotals[game.Players[caller].Id] += 50;
            }

            foreach (var score in result.Scores)
            {
                var expectedAward = score.PlayerId == game.Players[starter].Id ? (success ? 52 : 1)
                    : !success && score.PlayerId == game.Players[caller].Id ? 50 : 0;
                Assert.Equal(expectedAward, score.PointsAwarded);
                Assert.Equal(expectedTotals[score.PlayerId], score.TotalPoints);
                Assert.Equal(score.TotalPoints, completed.Game.Scores[score.PlayerId].Points);
            }
            var next = TurnTestGame.Accepted(await game.Engine.NextRoundAsync(game.Id, result.RoundId, completed.Version));
            Assert.NotEqual(result.RoundId, next.Game.CurrentRound!.Id);
            Assert.Equal(index + 2, next.Game.CurrentRound!.Number);
            Assert.Equal(game.Players, next.Game.Players);
            Assert.All(next.Players, player => Assert.Equal(1, player.ConnectionCount));
            Assert.Equal(game.Id, next.Id);
            Assert.Equal(completed.CreatedAt, next.CreatedAt);
            foreach (var previous in results)
            {
                var read = await game.Engine.GetRoundResultAsync(game.Id, previous.RoundId);
                Assert.True(read.Succeeded);
                Assert.Same(previous, read.RoundResult);
            }
        }
    }

    [Fact]
    public async Task ProgressionRequiresTheCurrentScoredResultAndRejectsStaleAndRepeatedRequests()
    {
        var game = await ChallengeTestGame.Create(required: 1);
        var pending = await game.State();
        Assert.Equal(GameEngineErrorCode.InvalidState,
            (await game.Engine.NextRoundAsync(game.Id, pending.Game.CurrentRound!.Id, pending.Version)).Error!.Code);
        Assert.Equal(GameEngineErrorCode.NotFound,
            (await game.Engine.GetRoundResultAsync(game.Id, pending.Game.CurrentRound.Id)).Error!.Code);
        var completed = TurnTestGame.Accepted(await game.Submit(0, "Paris"));
        Assert.Equal(GameEngineErrorCode.InvalidAction,
            (await game.Engine.NextRoundAsync(game.Id, Guid.Empty, completed.Version)).Error!.Code);
        Assert.Equal(GameEngineErrorCode.InvalidAction,
            (await game.Engine.NextRoundAsync(game.Id, pending.Game.CurrentRound.Id, -1)).Error!.Code);
        Assert.Equal(GameEngineErrorCode.InvalidState,
            (await game.Engine.NextRoundAsync(game.Id, Guid.NewGuid(), completed.Version)).Error!.Code);
        Assert.Equal(GameEngineErrorCode.StaleState,
            (await game.Engine.NextRoundAsync(game.Id, pending.Game.CurrentRound.Id, pending.Version)).Error!.Code);
        Assert.Equal(GameEngineErrorCode.InvalidState,
            (await game.Engine.PlaceBidAsync(game.Id, game.Players[0].Id, 2, await game.Context())).Error!.Code);
        Assert.Equal(GameEngineErrorCode.InvalidState,
            (await game.Engine.SelectCategoryAsync(game.Id, game.Players[0].Id, "capitals", await game.Context())).Error!.Code);
        Assert.Equal(GameEngineErrorCode.InvalidState,
            (await game.Engine.CallBluffAsync(game.Id, game.Players[1].Id, await game.Context())).Error!.Code);
        Assert.False((await game.Submit(0, "Berlin")).Succeeded);
        Assert.False((await game.Engine.ResolveChallengeAsync(game.Id)).Succeeded);
        Assert.Equal(completed.Version, (await game.State()).Version);
        var next = TurnTestGame.Accepted(await game.Engine.NextRoundAsync(game.Id, pending.Game.CurrentRound.Id, completed.Version));
        Assert.Equal(GameEngineErrorCode.StaleState,
            (await game.Engine.NextRoundAsync(game.Id, pending.Game.CurrentRound.Id, completed.Version)).Error!.Code);
        Assert.Equal(GameEngineErrorCode.InvalidState,
            (await game.Engine.NextRoundAsync(game.Id, pending.Game.CurrentRound.Id, next.Version)).Error!.Code);
        Assert.Equal(next.Version, (await game.State()).Version);
    }

    [Fact]
    public async Task ACompletedRoundWithoutScoringCannotAdvance()
    {
        var game = await ChallengeTestGame.Create();
        var completed = await game.Store.UpdateAsync(game.Id, session =>
        {
            var round = session.Game.CurrentRound!;
            round.Challenge!.Resolve(ChallengeOutcome.Failed);
            round.Complete();
        });
        Assert.Equal(GameEngineErrorCode.InvalidState,
            (await game.Engine.NextRoundAsync(game.Id, completed.Game.CurrentRound!.Id, completed.Version)).Error!.Code);
        Assert.Equal(completed.Version, (await game.State()).Version);
        Assert.Empty((await game.State()).Game.RoundResults);
    }

    [Fact]
    public async Task ResultsRemainReadableAfterMatchCompletionButCancelledRoundsHaveNoResult()
    {
        var game = await ChallengeTestGame.Create(required: 1);
        var completion = await game.Submit(0, "Paris");
        var result = completion.RoundResult!;
        TurnTestGame.Accepted(await game.Engine.CompleteAsync(game.Id));
        Assert.Same(result, (await game.Engine.GetRoundResultAsync(game.Id, result.RoundId)).RoundResult);
        Assert.False((await game.Engine.NextRoundAsync(game.Id)).Succeeded);
        Assert.Equal(GameEngineErrorCode.NotFound, (await game.Engine.GetRoundResultAsync(Guid.NewGuid(), result.RoundId)).Error!.Code);
        Assert.Equal(GameEngineErrorCode.NotFound, (await game.Engine.GetRoundResultAsync(game.Id, Guid.NewGuid())).Error!.Code);
        var cancelled = await ChallengeTestGame.Create();
        var state = TurnTestGame.Accepted(await cancelled.Engine.CancelAsync(cancelled.Id));
        Assert.Empty(state.Game.RoundResults);
        Assert.Null((await cancelled.Engine.GetStateAsync(cancelled.Id)).RoundResult);
    }
}
