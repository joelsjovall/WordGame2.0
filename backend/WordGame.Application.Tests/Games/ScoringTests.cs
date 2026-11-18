using WordGame.Application.Categories;
using WordGame.Application.Games;
using WordGame.Domain;

namespace WordGame.Application.Tests.Games;

public sealed class ScoringTests
{
    [Fact]
    public async Task ScoresAccumulateAcrossRoundsAndRetainCapturedWordValue()
    {
        var provider = new TestCategoryProvider();
        var multipliers = Enum.GetValues<CategoryDifficulty>().ToDictionary(difficulty => difficulty, difficulty => 4 + (int)difficulty);
        var game = await TurnTestGame.Create(2, provider, new CategorySelectionRules(null, scoreMultipliers: multipliers));
        await game.Select();
        // Provider metadata changing after selection cannot alter this round's captured value.
        provider.Definitions["cars"] = new CategoryDefinition(new Category("cars", "Cars", CategoryDifficulty.Hard), [new AcceptedAnswer("Volvo")]);
        await game.Bid(0, 1);
        await game.Bluff(1);
        TurnTestGame.Accepted(await game.Submit(0, "Volvo"));
        var next = TurnTestGame.Accepted(await game.Engine.NextRoundAsync(game.Id));
        var starter = Array.FindIndex(game.Players, player => player.Id == next.Game.CurrentRound!.Turn.PlayerId);
        await game.Select(starter);
        await game.Bid(starter, 1);
        await game.Bluff(1 - starter);
        var final = TurnTestGame.Accepted(await game.Submit(starter, "Volvo"));
        Assert.Equal(110, final.Game.Scores.Values.Sum(score => score.Points));
        Assert.Equal(54, final.Game.Scores[game.Players[0].Id].Points);
        Assert.Equal(56, final.Game.Scores[game.Players[1].Id].Points);
    }

    [Fact]
    public async Task CancelledChallengeAwardsNothing()
    {
        var game = await ChallengeTestGame.Create();
        await game.Submit(0, "Paris");
        TurnTestGame.Accepted(await game.Engine.LeaveAsync(game.Id, game.Players[0].Id, await game.Context()));
        var state = await game.State();
        Assert.Equal(ChallengeOutcome.Cancelled, state.Game.CurrentRound!.Challenge!.Outcome);
        Assert.All(state.Game.Scores.Values, score => Assert.Equal(0, score.Points));
    }

    [Theory]
    [InlineData(CategoryDifficulty.Easy, 1)]
    [InlineData(CategoryDifficulty.Medium, 2)]
    [InlineData(CategoryDifficulty.Hard, 3)]
    public async Task DifficultyControlsWordPointsAndCompletionAwardsOnlyOnce(CategoryDifficulty difficulty, int multiplier)
    {
        var definition = new CategoryDefinition(new Category("cars", "Cars", difficulty), [new AcceptedAnswer("Volvo")]);
        var game = await TurnTestGame.Create(provider: new TestCategoryProvider(definition));
        await game.Select();
        await game.Bid(0, 1);
        await game.Bluff(1);
        var result = TurnTestGame.Accepted(await game.Submit(0, "Volvo"));
        Assert.Equal(50 + multiplier, result.Game.Scores[game.Players[0].Id].Points);
        Assert.Equal(0, result.Game.Scores[game.Players[1].Id].Points);
        Assert.Equal(0, result.Game.Scores[game.Players[2].Id].Points);
        Assert.False((await game.Engine.ResolveChallengeAsync(game.Id)).Succeeded);
        Assert.False((await game.Submit(0, "Volvo")).Succeeded);
        Assert.Equal(result.Version, (await game.State()).Version);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task FailureAwardsOnlyUniqueWordsToBidderAndBonusToCaller(int players)
    {
        var game = await ChallengeTestGame.Create(players);
        await game.Submit(0, "Copenhagen");
        await game.Submit(0, "Köpenhamn");
        await game.Submit(0, "Unknown");
        Assert.All((await game.State()).Game.Scores.Values, score => Assert.Equal(0, score.Points));
        game.Clock.Advance(TurnTestGame.Duration);
        var result = TurnTestGame.Accepted(await game.Engine.ResolveChallengeAsync(game.Id));
        Assert.Equal(1, result.Game.Scores[game.Players[0].Id].Points);
        Assert.Equal(50, result.Game.Scores[game.Players[1].Id].Points);
        Assert.All(result.Game.Scores.Values.Where(score => !game.Players.Take(2).Any(player => player.Id == score.PlayerId)),
            score => Assert.Equal(0, score.Points));
    }

    [Fact]
    public async Task OverflowRollsBackFinalWordOutcomeAndActionId()
    {
        var multipliers = Enum.GetValues<CategoryDifficulty>().ToDictionary(difficulty => difficulty, _ => int.MaxValue);
        var game = await TurnTestGame.Create(rules: new CategorySelectionRules(null, scoreMultipliers: multipliers), settings: MatchSettings.Party(1));
        await game.Select();
        await game.Bid(0, 1);
        await game.Bluff(1);
        var before = await game.State();
        var context = await game.Context();
        Assert.Equal(GameEngineErrorCode.InvalidState, (await game.Submit(0, "Volvo", context)).Error!.Code);
        var after = await game.State();
        Assert.Equal(before.Version, after.Version);
        Assert.Equal(ChallengeOutcome.Pending, after.Game.CurrentRound!.Challenge!.Outcome);
        Assert.Empty(after.Game.CurrentRound.Challenge.Submissions);
        Assert.Null(after.Game.Result);
        Assert.All(after.Game.Scores.Values, score => Assert.Equal(0, score.Points));
        Assert.Equal(GameEngineErrorCode.InvalidState, (await game.Submit(0, "Volvo", context)).Error!.Code);
    }
}
