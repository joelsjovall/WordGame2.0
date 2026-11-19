using WordGame.Application.Categories;
using WordGame.Application.Games;
using WordGame.Domain;

namespace WordGame.Application.Tests.Games;

public sealed class MatchRuleSettingsTests
{
    [Fact]
    public async Task ChallengeUsesFrozenMatchDurationInsteadOfGlobalDefault()
    {
        var game = await ChallengeTestGame.Create(duration: TimeSpan.FromSeconds(30),
            settings: MatchSettings.Party(6, TimeSpan.FromSeconds(75), [CategoryDifficulty.Easy]));
        var challenge = (await game.State()).Game.CurrentRound!.Challenge!;
        Assert.Equal(TimeSpan.FromSeconds(75), challenge.Deadline - challenge.StartedAt);
    }

    [Fact]
    public async Task BothManualAndAutomaticCategorySelectionRespectFrozenDifficulties()
    {
        var provider = new TestCategoryProvider(CategorySelectionTests.Definition("a-easy", CategoryDifficulty.Easy),
            CategorySelectionTests.Definition("z-hard", CategoryDifficulty.Hard));
        var game = await TurnTestGame.Create(provider: provider, rules: new CategorySelectionRules(TimeSpan.FromSeconds(45)),
            settings: MatchSettings.Party(4, TimeSpan.FromSeconds(30), [CategoryDifficulty.Hard]));
        var rejected = await game.Engine.SelectCategoryAsync(game.Id, game.Players[0].Id, "a-easy", await game.Context());
        Assert.Equal(GameEngineErrorCode.InvalidState, rejected.Error!.Code);
        var before = await game.State();
        game.Clock.Advance(TimeSpan.FromSeconds(45));
        var result = TurnTestGame.Accepted(await game.Categories.ExpireAsync(game.Id, before.Version));
        Assert.Equal("z-hard", result.Game.CurrentRound!.Category!.Id);
    }
}
