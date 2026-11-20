using WordGame.Application.Games;
using WordGame.Application.Sessions;
using WordGame.Domain;

namespace WordGame.Application.Tests.Games;

public sealed class OnlineForfeitTests
{
    [Fact]
    public async Task LeadingPlayerCanForfeitWithoutChangingScoresOrAwardingThemselvesTheWin()
    {
        var game = await ChallengeTestGame.Create(players: 2, required: 1, settings: MatchSettings.Online());
        TurnTestGame.Accepted(await game.Submit(0, "Stockholm"));
        TurnTestGame.Accepted(await game.Engine.NextRoundAsync(game.Id));
        var before = await game.State();
        Assert.True(before.Game.Scores[game.Players[0].Id].Points > before.Game.Scores[game.Players[1].Id].Points);
        var ended = TurnTestGame.Accepted(await game.Engine.ForfeitAsync(game.Id, game.Players[0].Id, await game.Context()));
        Assert.Equal(SessionStatus.Completed, ended.Status);
        Assert.Equal(MatchEndReason.Forfeit, ended.Game.Result!.Reason);
        Assert.Equal(game.Players[1].Id, Assert.Single(ended.Game.Result.WinnerIds));
        Assert.Equal(1, ended.Game.Result.CompletedRoundCount);
        Assert.Equal(before.Game.Scores.Values, ended.Game.Scores.Values);
        Assert.True(ended.Players[0].HasLeft);
        Assert.Equal(0, ended.Players[0].ConnectionCount);
        Assert.False((await game.Engine.ConnectAsync(game.Id, game.Players[0].Id, Guid.NewGuid())).Succeeded);
        Assert.False((await game.Engine.ForfeitAsync(game.Id, game.Players[1].Id, await game.Context())).Succeeded);
        Assert.Same(ended.Game.Result, (await game.State()).Game.Result);
    }

    [Fact]
    public async Task ReconnectionKeepsTheChallengeAndItsOriginalDeadlineAndSubmissions()
    {
        var game = await ChallengeTestGame.Create(players: 2, required: 2, duration: TimeSpan.FromSeconds(20), settings: MatchSettings.Online());
        TurnTestGame.Accepted(await game.Submit(0, "Stockholm"));
        var before = await game.State();
        await game.Disconnect(0);
        Assert.Equal(SessionStatus.Active, (await game.State()).Status);
        game.Clock.Advance(TimeSpan.FromSeconds(5));
        var restored = TurnTestGame.Accepted(await game.Engine.ConnectAsync(game.Id, game.Players[0].Id, Guid.NewGuid()));
        Assert.Equal(before.Game.CurrentRound!.Id, restored.Game.CurrentRound!.Id);
        Assert.Equal(before.Game.CurrentRound.Challenge!.Deadline, restored.Game.CurrentRound.Challenge!.Deadline);
        Assert.Single(restored.Game.CurrentRound.Challenge.Submissions);
        Assert.Equal(before.Game.Scores.Values, restored.Game.Scores.Values);
        game.Clock.Advance(TimeSpan.FromSeconds(15));
        var resolved = TurnTestGame.Accepted(await game.Engine.ResolveChallengeAsync(game.Id));
        Assert.Equal(ChallengeOutcome.Failed, resolved.Game.CurrentRound!.Challenge!.Outcome);
        Assert.Equal(SessionStatus.Active, resolved.Status);
    }

    [Theory]
    [InlineData(29, SessionStatus.Active)]
    [InlineData(30, SessionStatus.Completed)]
    public async Task ReconnectionHonorsTheExactGraceBoundary(int delay, SessionStatus expected)
    {
        var game = await TurnTestGame.Create(2, settings: MatchSettings.Online());
        await game.Disconnect(0);
        game.Clock.Advance(TimeSpan.FromSeconds(delay));
        var state = TurnTestGame.Accepted(await game.Engine.ConnectAsync(game.Id, game.Players[0].Id, Guid.NewGuid()));
        Assert.Equal(expected, state.Status);
        Assert.Equal(expected == SessionStatus.Active ? 1 : 0, state.Players[0].ConnectionCount);
        if (expected == SessionStatus.Completed)
        {
            Assert.True(state.Players[0].HasLeft);
            Assert.Equal(MatchEndReason.ReconnectionExpired, state.Game.Result!.Reason);
        }
    }

    [Fact]
    public async Task LateAnswerCommitsTheTimeoutOutcomeWithoutAcceptingAnotherWord()
    {
        var game = await ChallengeTestGame.Create(players: 2, required: 2, duration: TimeSpan.FromMinutes(1), settings: MatchSettings.Online());
        TurnTestGame.Accepted(await game.Submit(0, "Stockholm"));
        await game.Disconnect(0);
        game.Clock.Advance(TimeSpan.FromSeconds(30));
        var rejected = await game.Submit(0, "Paris");
        Assert.Equal(GameEngineErrorCode.PlayerUnavailable, rejected.Error!.Code);
        var ended = await game.State();
        Assert.Equal(SessionStatus.Completed, ended.Status);
        Assert.Equal(game.Players[1].Id, Assert.Single(ended.Game.Result!.WinnerIds));
        Assert.Single(ended.Game.CurrentRound!.Challenge!.Submissions);
        Assert.Equal(ChallengeOutcome.Cancelled, ended.Game.CurrentRound.Challenge.Outcome);
        Assert.Empty(ended.Game.RoundResults);
    }

    [Fact]
    public async Task BothExpiredPlayersAbandonWithoutAwardingAnArbitraryWinner()
    {
        var game = await TurnTestGame.Create(2, settings: MatchSettings.Online());
        await game.Disconnect(0); await game.Disconnect(1);
        game.Clock.Advance(TimeSpan.FromSeconds(30));
        var state = TurnTestGame.Accepted(await game.Engine.ReconcileConnectionsAsync(game.Id, (await game.State()).Version));
        Assert.Equal(SessionStatus.Abandoned, state.Status);
        Assert.Null(state.Game.Result);
    }

    [Fact]
    public async Task OnlineForfeitDoesNotChangePartyRules()
    {
        var game = await TurnTestGame.Create(2);
        Assert.False((await game.Engine.ForfeitAsync(game.Id, game.Players[0].Id, await game.Context())).Succeeded);
        Assert.Equal(SessionStatus.Active, (await game.State()).Status);
    }
}
