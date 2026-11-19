using WordGame.Application.Sessions;
using WordGame.Domain;

namespace WordGame.Application.Tests.Games;

public sealed class ReconnectionTests
{
    [Fact]
    public async Task ReconnectRestoresScoredStateRoundAndTurnWithoutAddingPlayers()
    {
        var game = await ChallengeTestGame.Create(required: 1, settings: MatchSettings.Party(4));
        TurnTestGame.Accepted(await game.Submit(0, "Stockholm"));
        TurnTestGame.Accepted(await game.Engine.NextRoundAsync(game.Id));
        var before = await game.State();
        Assert.True(before.Game.Scores[game.Players[0].Id].Points > 0);
        await game.Disconnect(0);
        game.Clock.Advance(TurnTestGame.Grace - TimeSpan.FromTicks(1));
        var restored = TurnTestGame.Accepted(await game.Engine.ConnectAsync(game.Id, game.Players[0].Id, Guid.NewGuid()));
        Assert.Equal(before.Game.CurrentRound!.Id, restored.Game.CurrentRound!.Id);
        Assert.Equal(before.Game.CurrentRound.Turn, restored.Game.CurrentRound.Turn);
        Assert.Equal(before.Game.Scores.Values, restored.Game.Scores.Values);
        Assert.Equal(before.Game.Players.Select(player => player.Id), restored.Game.Players.Select(player => player.Id));
        Assert.Equal(2, restored.Game.Players.Count);
        Assert.Equal(1, restored.Players[0].ConnectionCount);
    }

    [Fact]
    public async Task ChallengeTimerAndAcceptedWordsDoNotResetOnReconnect()
    {
        var game = await ChallengeTestGame.Create(players: 3, required: 2);
        TurnTestGame.Accepted(await game.Submit(0, "Stockholm"));
        var before = (await game.State()).Game.CurrentRound!.Challenge!;
        await game.Disconnect(0);
        game.Clock.Advance(TimeSpan.FromSeconds(5));
        var restored = TurnTestGame.Accepted(await game.Engine.ConnectAsync(game.Id, game.Players[0].Id, Guid.NewGuid()));
        var challenge = restored.Game.CurrentRound!.Challenge!;
        Assert.Equal(before.Id, challenge.Id);
        Assert.Equal(before.Deadline, challenge.Deadline);
        Assert.Single(challenge.Submissions);
        game.Clock.Advance(challenge.Deadline - game.Clock.GetUtcNow());
        var result = TurnTestGame.Accepted(await game.Engine.ResolveChallengeAsync(game.Id));
        Assert.Equal(ChallengeOutcome.Failed, result.Game.CurrentRound!.Challenge!.Outcome);
    }

    [Fact]
    public async Task ReconnectionAtExpiredGraceCannotReviveATwoPlayerMatchBeforeCleanup()
    {
        var game = await TurnTestGame.Create(count: 2);
        await game.Disconnect(0);
        game.Clock.Advance(TurnTestGame.Grace);
        var late = TurnTestGame.Accepted(await game.Engine.ConnectAsync(game.Id, game.Players[0].Id, Guid.NewGuid()));
        Assert.Equal(SessionStatus.Abandoned, late.Status);
        Assert.Equal(0, late.Players[0].ConnectionCount);
        Assert.False(late.Players[0].HasLeft);
        Assert.False((await game.Engine.ConnectAsync(game.Id, game.Players[0].Id, Guid.NewGuid())).Succeeded);
    }

    [Fact]
    public async Task LateNonessentialPlayerCanReturnButCannotRestoreAnExpiredTurn()
    {
        var game = await TurnTestGame.Create(count: 3);
        await game.Disconnect(0);
        game.Clock.Advance(TurnTestGame.Grace);
        var restored = TurnTestGame.Accepted(await game.Engine.ConnectAsync(game.Id, game.Players[0].Id, Guid.NewGuid()));
        Assert.Equal(SessionStatus.Active, restored.Status);
        Assert.Equal(game.Players[1].Id, restored.Game.CurrentRound!.Turn.PlayerId);
        Assert.Equal(1, restored.Players[0].ConnectionCount);
    }
}
