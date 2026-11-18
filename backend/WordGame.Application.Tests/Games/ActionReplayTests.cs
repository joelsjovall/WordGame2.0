using WordGame.Application.Games;

namespace WordGame.Application.Tests.Games;

public sealed class ActionReplayTests
{
    [Fact]
    public async Task DuplicateIdsCannotBeReusedInAnotherPhaseRoundOrByAnotherPlayer()
    {
        var game = await TurnTestGame.Create();
        var context = await game.Context();
        TurnTestGame.Accepted(await game.Engine.SelectCategoryAsync(game.Id, game.Players[0].Id, TurnTestGame.Category, context));
        var version = (await game.State()).Version;
        Assert.Equal(GameEngineErrorCode.StaleState,
            (await game.Engine.PlaceBidAsync(game.Id, game.Players[0].Id, 1, context)).Error!.Code);
        Assert.Equal(GameEngineErrorCode.DuplicateAction,
            (await game.Engine.PlaceBidAsync(game.Id, game.Players[0].Id, 1, await game.Context(context.ActionId))).Error!.Code);
        Assert.Equal(version, (await game.State()).Version);
        await game.Bid(0, 1);
        await game.Bluff(1);
        game.Clock.Advance(TurnTestGame.Duration);
        TurnTestGame.Accepted(await game.Engine.ResolveChallengeAsync(game.Id));
        TurnTestGame.Accepted(await game.Engine.NextRoundAsync(game.Id));
        var before = await game.State();
        Assert.Equal(GameEngineErrorCode.DuplicateAction,
            (await game.Engine.SelectCategoryAsync(game.Id, game.Players[1].Id, TurnTestGame.Category, await game.Context(context.ActionId))).Error!.Code);
        Assert.Equal(before.Version, (await game.State()).Version);
        Assert.Equal(before.Game.CurrentRound!.Turn, (await game.State()).Game.CurrentRound!.Turn);
    }

    [Fact]
    public async Task StaleAndOutOfTurnCommandsDoNotConsumeAnActionId()
    {
        var game = await TurnTestGame.Create();
        var context = await game.Context();
        var wrongTurn = await game.Engine.SelectCategoryAsync(game.Id, game.Players[1].Id, TurnTestGame.Category, context);
        Assert.Equal(GameEngineErrorCode.NotYourTurn, wrongTurn.Error!.Code);
        TurnTestGame.Accepted(await game.Engine.SelectCategoryAsync(game.Id, game.Players[0].Id, TurnTestGame.Category, context));
        var bid = await game.Context();
        TurnTestGame.Accepted(await game.Engine.ConnectAsync(game.Id, game.Players[0].Id, Guid.NewGuid()));
        Assert.Equal(GameEngineErrorCode.StaleState,
            (await game.Engine.PlaceBidAsync(game.Id, game.Players[0].Id, 1, bid)).Error!.Code);
        TurnTestGame.Accepted(await game.Engine.PlaceBidAsync(game.Id, game.Players[0].Id, 1, await game.Context(bid.ActionId)));
    }

    [Fact]
    public async Task MalformedActionContextCannotChangeState()
    {
        var game = await TurnTestGame.Create();
        var before = await game.State();
        foreach (var context in new PlayerActionContext[] { new(Guid.Empty, before.Version), new(Guid.NewGuid(), -1), null! })
        {
            Assert.Equal(GameEngineErrorCode.InvalidAction,
                (await game.Engine.SelectCategoryAsync(game.Id, game.Players[0].Id, TurnTestGame.Category, context)).Error!.Code);
        }

        Assert.Equal(before.Version, (await game.State()).Version);
    }
}
