using WordGame.Application.Games;
using WordGame.Application.Sessions;
using WordGame.Domain;

namespace WordGame.Application.Tests.Games;

public sealed class MatchCompletionTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task DefaultPartyMatchesAutomaticallyFinishAfterTwoRoundsPerPlayer(int players)
    {
        var game = await TurnTestGame.Create(players);
        await PlayMatch(game, round => round % players == 0);
        var state = await game.State();
        Assert.Equal(SessionStatus.Completed, state.Status);
        Assert.Equal(players * 2, state.Game.Rounds.Count);
        Assert.Equal(game.Clock.GetUtcNow(), state.EndedAt);
        Assert.Null(state.CategorySelectionDeadline);
        var result = (await game.Engine.GetStateAsync(game.Id)).MatchResult!;
        Assert.False(result.IsTie);
        Assert.Equal(game.Players[0].Id, Assert.Single(result.WinnerIds));
        Assert.Equal(players, result.Standings.Count);
        Assert.All(result.Standings, standing => Assert.Equal(state.Game.Scores[standing.PlayerId].Points, standing.Score));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public async Task PartyCanChooseACustomLength(int rounds)
    {
        var game = await TurnTestGame.Create(2, settings: MatchSettings.Party(rounds));
        await PlayMatch(game, _ => true);
        Assert.Equal(rounds, (await game.State()).Game.Result!.CompletedRoundCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EqualTopScoresAreSharedWinsWithoutExtraRounds(bool online)
    {
        var game = await TurnTestGame.Create(2, settings: online ? MatchSettings.Online() : MatchSettings.Party(2));
        await PlayMatch(game, _ => true);
        var result = (await game.Engine.GetStateAsync(game.Id)).MatchResult!;
        Assert.Equal(online ? MatchFormat.Online : MatchFormat.Party, result.Format);
        Assert.Equal(online ? 6 : 2, result.CompletedRoundCount);
        Assert.True(result.IsTie);
        Assert.Equal(game.Players.Select(player => player.Id), result.WinnerIds);
        Assert.All(result.Standings, standing => Assert.Equal(1, standing.Rank));
    }

    [Fact]
    public async Task CompletedMatchRejectsGameplayAndKeepsResultsAcrossPresenceChanges()
    {
        var game = await TurnTestGame.Create(2, settings: MatchSettings.Party(1));
        await PlayMatch(game, _ => true);
        var before = await game.State();
        var context = await game.Context();
        var id = game.Players[0].Id;
        Func<Task<GameEngineResult>>[] actions =
        [
            () => game.Engine.SelectCategoryAsync(game.Id, id, "cars", context),
            () => game.Engine.PlaceBidAsync(game.Id, id, 2, context),
            () => game.Engine.CallBluffAsync(game.Id, id, context),
            () => game.Engine.SubmitWordAsync(game.Id, id, "Volvo", context),
            () => game.Engine.ResolveChallengeAsync(game.Id),
            () => game.Engine.NextRoundAsync(game.Id),
            () => game.Engine.NextRoundAsync(game.Id, before.Game.CurrentRound!.Id, before.Version),
            () => game.Engine.StartAsync(game.Id), () => game.Engine.InitializeAsync(game.Id),
            () => game.Engine.CompleteAsync(game.Id), () => game.Engine.CancelAsync(game.Id),
            () => game.Engine.LeaveAsync(game.Id, id, context)
        ];
        foreach (var action in actions)
        {
            Assert.Equal(GameEngineErrorCode.InvalidState, (await action()).Error!.Code);
            Assert.Equal(before.Version, (await game.State()).Version);
        }
        game.Clock.Advance(TimeSpan.FromSeconds(1));
        await game.Disconnect(0);
        TurnTestGame.Accepted(await game.Engine.ConnectAsync(game.Id, id, Guid.NewGuid()));
        var read = await game.Engine.GetStateAsync(game.Id);
        Assert.Same(before.Game.Result, read.MatchResult);
        Assert.Equal(before.EndedAt, read.State!.EndedAt);
        Assert.NotNull((await game.Engine.GetRoundResultAsync(game.Id, before.Game.CurrentRound!.Id)).RoundResult);
    }

    [Fact]
    public async Task EarlyCompletionAndInvalidOnlineRostersAreRejectedAndCancellationHasNoWinner()
    {
        var game = await TurnTestGame.Create(2);
        Assert.Equal(GameEngineErrorCode.InvalidState, (await game.Engine.CompleteAsync(game.Id)).Error!.Code);
        await FinishRound(game, true);
        Assert.Equal(GameEngineErrorCode.InvalidState, (await game.Engine.CompleteAsync(game.Id)).Error!.Code);
        TurnTestGame.Accepted(await game.Engine.CancelAsync(game.Id));
        Assert.Null((await game.Engine.GetStateAsync(game.Id)).MatchResult);
        var invalidPlayers = Enumerable.Range(0, 3).Select(index => new Player(Guid.NewGuid(), $"Player {index}"));
        Assert.Equal(GameEngineErrorCode.InvalidAction, (await game.Engine.CreateAsync(invalidPlayers, MatchSettings.Online())).Error!.Code);
    }

    private static async Task PlayMatch(TurnTestGame game, Func<int, bool> success)
    {
        var count = (await game.State()).Game.Settings.RoundCount;
        for (var round = 0; round < count; round++)
        {
            var result = await FinishRound(game, success(round));
            Assert.NotNull(result.RoundResult);
            if (round == count - 1)
            {
                Assert.NotNull(result.MatchResult);
                Assert.Equal(SessionStatus.Completed, result.State!.Status);
            }
            else
            {
                Assert.Null(result.MatchResult);
                Assert.Equal(SessionStatus.Active, result.State!.Status);
                TurnTestGame.Accepted(await game.Engine.NextRoundAsync(game.Id, result.RoundResult!.RoundId, result.State.Version));
            }
        }
    }

    private static async Task<GameEngineResult> FinishRound(TurnTestGame game, bool success)
    {
        var state = await game.State();
        var starter = Array.FindIndex(game.Players, player => player.Id == state.Game.CurrentRound!.StartingPlayerId);
        await game.Select(starter);
        await game.Bid(starter, 1);
        await game.Bluff((starter + 1) % game.Players.Length);
        if (success)
        {
            return await game.Submit(starter, "Volvo");
        }

        game.Clock.Advance(TurnTestGame.Duration);
        return await game.Engine.ResolveChallengeAsync(game.Id);
    }
}
