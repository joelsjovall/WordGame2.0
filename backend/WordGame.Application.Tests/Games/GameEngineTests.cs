using WordGame.Application.Games;
using WordGame.Application.Sessions;
using WordGame.Domain;

namespace WordGame.Application.Tests.Games;

public sealed class GameEngineTests
{
    private static readonly Category Category = new("cars", "Cars", CategoryDifficulty.Easy);
    private static readonly TimeSpan Duration = TimeSpan.FromSeconds(30);
    private readonly EngineTestClock clock = new();
    private readonly EngineTestStore store;
    private readonly GameEngine engine;

    public GameEngineTests()
    {
        store = new EngineTestStore(clock);
        engine = TestCategoryProvider.Engine(store, new SessionRetention(TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(5)));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task BothModesUseTheFullLifecycleWithSequentialRoundsAndTrackedPlayers(int count)
    {
        var players = Players(count);
        var created = State(await engine.CreateAsync(players, MatchSettings.Party(2)));
        Assert.Equal(SessionStatus.Waiting, created.Status);
        Assert.Equal(GameStatus.WaitingToStart, created.Game.Status);
        Assert.Null(created.Game.CurrentRound);
        Assert.All(created.Game.Scores.Values, score => Assert.Equal(0, score.Points));
        Assert.Equal(SessionStatus.Starting, State(await engine.InitializeAsync(created.Id)).Status);

        var started = State(await engine.StartAsync(created.Id));
        Assert.Equal(SessionStatus.Active, started.Status);
        Assert.Equal(GameStatus.InProgress, started.Game.Status);
        Assert.Equal(RoundStatus.CategorySelection, started.Game.CurrentRound!.Status);
        Assert.Equal(1, started.Game.CurrentRound.Number);
        Assert.Equal(players[0].Id, started.Game.CurrentRound.Turn.PlayerId);

        var first = await FinishRound(created.Id, players[0].Id, players[1].Id);
        var next = State(await engine.NextRoundAsync(created.Id));
        Assert.Equal(2, next.Game.CurrentRound!.Number);
        Assert.NotEqual(first.Game.CurrentRound!.Id, next.Game.CurrentRound.Id);
        Assert.Equal(players[1].Id, next.Game.CurrentRound.Turn.PlayerId);
        await FinishRound(created.Id, players[1].Id, players[(2 % count)].Id);
        var completed = State(await engine.GetStateAsync(created.Id));
        await Reject(created.Id, () => engine.CompleteAsync(created.Id), GameEngineErrorCode.InvalidState);
        Assert.Equal(SessionStatus.Completed, completed.Status);
        Assert.Equal(GameStatus.Completed, completed.Game.Status);
        Assert.Equal(clock.GetUtcNow(), completed.EndedAt);
        Assert.Equal(2, completed.Game.Rounds.Count);
        await Reject(created.Id, () => engine.NextRoundAsync(created.Id), GameEngineErrorCode.InvalidState);
        await Reject(created.Id, () => engine.InitializeAsync(created.Id), GameEngineErrorCode.InvalidState);
        await Reject(created.Id, () => engine.CancelAsync(created.Id), GameEngineErrorCode.InvalidState);
    }

    [Fact]
    public async Task InvalidTransitionsArgumentsAndPermissionsLeaveStateAndRevisionUnchanged()
    {
        var players = Players();
        var id = State(await engine.CreateAsync(players)).Id;
        await Reject(id, () => engine.StartAsync(id), GameEngineErrorCode.InvalidState);
        await Reject(id, () => engine.CompleteAsync(id), GameEngineErrorCode.InvalidState);
        State(await engine.InitializeAsync(id));
        await Reject(id, () => engine.InitializeAsync(id), GameEngineErrorCode.InvalidState);
        State(await engine.StartAsync(id));
        await Reject(id, () => engine.StartAsync(id), GameEngineErrorCode.InvalidState);
        await Reject(id, () => engine.NextRoundAsync(id), GameEngineErrorCode.InvalidState);
        await Reject(id, () => engine.ResolveChallengeAsync(id), GameEngineErrorCode.InvalidState);
        await Reject(id, () => PlaceBidAsync(id, players[0].Id, 1), GameEngineErrorCode.InvalidState);
        await Reject(id, () => SelectCategoryAsync(id, Guid.NewGuid(), Category), GameEngineErrorCode.PlayerNotInGame);
        await Reject(id, () => SelectCategoryAsync(id, players[1].Id, Category), GameEngineErrorCode.NotYourTurn);
        await Reject(id, () => SelectCategoryAsync(id, players[0].Id, null!), GameEngineErrorCode.InvalidAction);
        State(await SelectCategoryAsync(id, players[0].Id, Category));
        await Reject(id, () => SelectCategoryAsync(id, players[0].Id, Category), GameEngineErrorCode.InvalidState);
        await Reject(id, () => CallBluffAsync(id, players[0].Id), GameEngineErrorCode.InvalidState);
        await Reject(id, () => PlaceBidAsync(id, players[0].Id, 0), GameEngineErrorCode.InvalidAction);
        State(await PlaceBidAsync(id, players[0].Id, 2));
        await Reject(id, () => PlaceBidAsync(id, players[0].Id, 3), GameEngineErrorCode.NotYourTurn);
        await Reject(id, () => PlaceBidAsync(id, players[1].Id, 2), GameEngineErrorCode.InvalidState);
        State(await CallBluffAsync(id, players[1].Id));
        await Reject(id, () => engine.ResolveChallengeAsync(id), GameEngineErrorCode.InvalidState);
        await Reject(id, () => engine.CompleteAsync(id), GameEngineErrorCode.InvalidState);
        clock.Advance(Duration);
        State(await engine.ResolveChallengeAsync(id));
        await Reject(id, () => engine.ResolveChallengeAsync(id), GameEngineErrorCode.InvalidState);
        State(await engine.NextRoundAsync(id));
    }

    [Fact]
    public async Task SuccessIsDerivedFromValidatedStoredWordsAndCannotBeChosenByACaller()
    {
        var players = Players();
        var id = await Start(players);
        State(await SelectCategoryAsync(id, players[0].Id, Category));
        State(await PlaceBidAsync(id, players[0].Id, 1));
        var challenge = State(await CallBluffAsync(id, players[1].Id)).Game.CurrentRound!.Challenge!;
        Assert.Equal(clock.GetUtcNow(), challenge.StartedAt);
        Assert.Equal(clock.GetUtcNow() + Duration, challenge.Deadline);

        var submission = await engine.SubmitWordAsync(id, players[0].Id, "Volvo", await Context(id));
        Assert.Equal(WordSubmissionStatus.Accepted, submission.SubmissionStatus);
        var resolved = State(submission);
        Assert.Equal(ChallengeOutcome.Succeeded, resolved.Game.CurrentRound!.Challenge!.Outcome);
        Assert.Equal(RoundStatus.Completed, resolved.Game.CurrentRound.Status);
        Assert.Equal(SessionStatus.Active, resolved.Status);
    }

    [Fact]
    public async Task SessionsAndReturnedSnapshotsAreIndependentEvenWithTheSameRoster()
    {
        var players = Players();
        var first = await Start(players);
        var second = State(await engine.CreateAsync(players));
        Assert.NotEqual(first, second.Id);
        State(await SelectCategoryAsync(first, players[0].Id, Category));
        var snapshot = State(await engine.GetStateAsync(first));
        snapshot.Game.Cancel();
        Assert.Equal(GameStatus.InProgress, State(await engine.GetStateAsync(first)).Game.Status);
        var untouched = State(await engine.GetStateAsync(second.Id));
        Assert.Equal(0, untouched.Version);
        Assert.Equal(SessionStatus.Waiting, untouched.Status);
        Assert.Null(untouched.Game.CurrentRound);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationEndsWaitingOrActiveGamesAndPreventsRestart(bool active)
    {
        var players = Players();
        var id = active ? await Start(players) : State(await engine.CreateAsync(players)).Id;
        if (active)
        {
            State(await SelectCategoryAsync(id, players[0].Id, Category));
            State(await PlaceBidAsync(id, players[0].Id, 1));
            State(await CallBluffAsync(id, players[1].Id));
        }
        var result = State(await engine.CancelAsync(id));
        Assert.Equal(SessionStatus.Abandoned, result.Status);
        Assert.Equal(GameStatus.Cancelled, result.Game.Status);
        Assert.Equal(clock.GetUtcNow(), result.EndedAt);
        if (active)
        {
            Assert.Equal(RoundStatus.Cancelled, result.Game.CurrentRound!.Status);
            Assert.Equal(ChallengeOutcome.Cancelled, result.Game.CurrentRound.Challenge!.Outcome);
        }
        await Reject(id, () => engine.InitializeAsync(id), GameEngineErrorCode.InvalidState);
        await Reject(id, () => engine.CancelAsync(id), GameEngineErrorCode.InvalidState);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    public async Task InvalidCreationReturnsStructuredRejection(int count)
    {
        var result = await engine.CreateAsync(Players(count));
        Assert.False(result.Succeeded);
        Assert.Null(result.State);
        Assert.Equal(GameEngineErrorCode.InvalidAction, result.Error!.Code);
    }

    [Fact]
    public async Task InvalidRosterEntriesReturnStructuredRejections()
    {
        var player = Players()[0];
        foreach (var roster in new IEnumerable<Player>[] { [player, player], [player, null!], null! })
        {
            var result = await engine.CreateAsync(roster);
            Assert.False(result.Succeeded);
            Assert.Null(result.State);
            Assert.Equal(GameEngineErrorCode.InvalidAction, result.Error!.Code);
        }
    }

    [Fact]
    public async Task MissingGamesAreStructuredAndRequestCancellationPropagates()
    {
        Assert.Equal(GameEngineErrorCode.NotFound, (await engine.InitializeAsync(Guid.NewGuid())).Error!.Code);
        Assert.Equal(GameEngineErrorCode.NotFound, (await engine.GetStateAsync(Guid.NewGuid())).Error!.Code);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => engine.CreateAsync(Players(), cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => engine.InitializeAsync(Guid.NewGuid(), cancellation.Token));
    }

    private async Task<GameEngineResult> SelectCategoryAsync(Guid id, Guid actor, Category category) =>
        await engine.SelectCategoryAsync(id, actor, category?.Id!, await Context(id));

    private async Task<GameEngineResult> PlaceBidAsync(Guid id, Guid actor, int count) =>
        await engine.PlaceBidAsync(id, actor, count, await Context(id));

    private async Task<GameEngineResult> CallBluffAsync(Guid id, Guid actor) =>
        await engine.CallBluffAsync(id, actor, await Context(id));

    private async Task<PlayerActionContext> Context(Guid id) => new(Guid.NewGuid(), State(await engine.GetStateAsync(id)).Version);

    private async Task<Guid> Start(Player[] players)
    {
        var id = State(await engine.CreateAsync(players)).Id;
        State(await engine.InitializeAsync(id));
        State(await engine.StartAsync(id));
        return id;
    }

    private async Task<SessionSnapshot> FinishRound(Guid id, Guid bidder, Guid caller)
    {
        Assert.Equal(RoundStatus.Bidding, State(await SelectCategoryAsync(id, bidder, Category)).Game.CurrentRound!.Status);
        var bidding = State(await PlaceBidAsync(id, bidder, 1));
        Assert.Equal(caller, bidding.Game.CurrentRound!.Turn.PlayerId);
        var challenging = State(await CallBluffAsync(id, caller));
        Assert.Equal(RoundStatus.Challenge, challenging.Game.CurrentRound!.Status);
        Assert.Equal(bidder, challenging.Game.CurrentRound.Turn.PlayerId);
        clock.Advance(Duration);
        var resolved = State(await engine.ResolveChallengeAsync(id));
        Assert.Equal(RoundStatus.Completed, resolved.Game.CurrentRound!.Status);
        Assert.Equal(ChallengeOutcome.Failed, resolved.Game.CurrentRound.Challenge!.Outcome);
        return resolved;
    }

    private async Task Reject(Guid id, Func<Task<GameEngineResult>> action, GameEngineErrorCode code)
    {
        var before = State(await engine.GetStateAsync(id));
        var result = await action();
        Assert.False(result.Succeeded);
        Assert.Null(result.State);
        Assert.Equal(code, result.Error!.Code);
        var after = State(await engine.GetStateAsync(id));
        Assert.Equal(before.Version, after.Version);
        Assert.Equal(before.UpdatedAt, after.UpdatedAt);
        Assert.Equal((before.Status, before.Game.Status, before.Game.Rounds.Count), (after.Status, after.Game.Status, after.Game.Rounds.Count));
        Assert.Equal(before.Game.CurrentRound?.Status, after.Game.CurrentRound?.Status);
        Assert.Equal(before.Game.CurrentRound?.Turn, after.Game.CurrentRound?.Turn);
        Assert.Equal(before.Game.CurrentRound?.HighestBid, after.Game.CurrentRound?.HighestBid);
        Assert.Equal(before.Game.CurrentRound?.Challenge?.Outcome, after.Game.CurrentRound?.Challenge?.Outcome);
    }

    private static SessionSnapshot State(GameEngineResult result)
    {
        Assert.True(result.Succeeded, result.Error?.Message);
        Assert.Null(result.Error);
        return Assert.IsType<SessionSnapshot>(result.State);
    }

    private static Player[] Players(int count = 2) => Enumerable.Range(0, count)
        .Select(index => new Player(Guid.NewGuid(), $"Player {index}")).ToArray();
}
