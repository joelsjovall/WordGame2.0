using WordGame.Application.Categories;
using WordGame.Application.Challenges;
using WordGame.Application.Games;
using WordGame.Application.Sessions;
using WordGame.Application.Words;
using WordGame.Domain;

namespace WordGame.Application.Tests.Games;

internal sealed class TurnTestGame
{
    internal static readonly TimeSpan Grace = TimeSpan.FromMinutes(2);
    internal static readonly TimeSpan Duration = TimeSpan.FromSeconds(30);
    internal static readonly Category Category = new("cars", "Cars", CategoryDifficulty.Easy);
    internal EngineTestClock Clock { get; } = new();
    internal EngineTestStore Store { get; }
    internal GameEngine Engine { get; }
    internal CategorySelectionService Categories { get; }
    internal ChallengeService Challenges { get; }
    internal Player[] Players { get; }
    internal Guid[] Connections { get; }
    internal Guid Id { get; private set; }

    private TurnTestGame(int count, TestCategoryProvider? provider, CategorySelectionRules? rules, ChallengeRules? challengeRules)
    {
        Store = new EngineTestStore(Clock);
        var retention = new SessionRetention(Grace, Grace, Grace);
        Categories = new CategorySelectionService(provider ?? new TestCategoryProvider(), rules ?? new CategorySelectionRules(null), Store, retention);
        Challenges = new ChallengeService(Store, retention, new WordValidationService(), challengeRules ?? new ChallengeRules(Duration));
        Engine = new GameEngine(Store, retention, Categories, Challenges);
        Players = Enumerable.Range(0, count).Select(index => new Player(Guid.NewGuid(), $"Player {index}")).ToArray();
        Connections = Players.Select(_ => Guid.NewGuid()).ToArray();
    }

    internal static async Task<TurnTestGame> Create(int count = 3, TestCategoryProvider? provider = null,
        CategorySelectionRules? rules = null, ChallengeRules? challengeRules = null, MatchSettings? settings = null)
    {
        var game = new TurnTestGame(count, provider, rules, challengeRules);
        game.Id = Accepted(settings is null ? await game.Engine.CreateAsync(game.Players) : await game.Engine.CreateAsync(game.Players, settings)).Id;
        for (var index = 0; index < count; index++)
        {
            Accepted(await game.Engine.ConnectAsync(game.Id, game.Players[index].Id, game.Connections[index]));
        }

        Accepted(await game.Engine.InitializeAsync(game.Id));
        Accepted(await game.Engine.StartAsync(game.Id));
        return game;
    }

    internal async Task<SessionSnapshot> State() => Accepted(await Engine.GetStateAsync(Id));
    internal async Task<PlayerActionContext> Context(Guid? actionId = null) => new(actionId ?? Guid.NewGuid(), (await State()).Version);
    internal async Task<SessionSnapshot> Select(int actor = 0) => Accepted(await Engine.SelectCategoryAsync(Id, Players[actor].Id, Category.Id, await Context()));
    internal async Task<SessionSnapshot> Bid(int actor, int count) => Accepted(await Engine.PlaceBidAsync(Id, Players[actor].Id, count, await Context()));
    internal async Task<SessionSnapshot> Bluff(int actor) => Accepted(await Engine.CallBluffAsync(Id, Players[actor].Id, await Context()));
    internal async Task<GameEngineResult> Submit(int actor, string word, PlayerActionContext? context = null) =>
        await Engine.SubmitWordAsync(Id, Players[actor].Id, word, context ?? await Context());
    internal async Task Disconnect(int player) => Accepted(await Engine.DisconnectAsync(Id, Players[player].Id, Connections[player]));
    internal Task<SessionSnapshot> Reconcile() => Store.UpdateAsync(Id, session => session.ReconcileTurns(Grace));

    internal static SessionSnapshot Accepted(GameEngineResult result)
    {
        Assert.True(result.Succeeded, result.Error?.Message);
        return Assert.IsType<SessionSnapshot>(result.State);
    }
}
