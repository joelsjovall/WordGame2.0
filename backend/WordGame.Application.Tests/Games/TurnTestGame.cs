using WordGame.Application.Games;
using WordGame.Application.Sessions;
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
    internal Player[] Players { get; }
    internal Guid[] Connections { get; }
    internal Guid Id { get; private set; }

    private TurnTestGame(int count)
    {
        Store = new EngineTestStore(Clock);
        Engine = new GameEngine(Store, Clock, new SessionRetention(Grace, Grace, Grace));
        Players = Enumerable.Range(0, count).Select(index => new Player(Guid.NewGuid(), $"Player {index}")).ToArray();
        Connections = Players.Select(_ => Guid.NewGuid()).ToArray();
    }

    internal static async Task<TurnTestGame> Create(int count = 3)
    {
        var game = new TurnTestGame(count);
        game.Id = Accepted(await game.Engine.CreateAsync(game.Players)).Id;
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
    internal async Task<SessionSnapshot> Select(int actor = 0) => Accepted(await Engine.SelectCategoryAsync(Id, Players[actor].Id, Category, await Context()));
    internal async Task<SessionSnapshot> Bid(int actor, int count) => Accepted(await Engine.PlaceBidAsync(Id, Players[actor].Id, count, await Context()));
    internal async Task<SessionSnapshot> Bluff(int actor) => Accepted(await Engine.CallBluffAsync(Id, Players[actor].Id, Duration, await Context()));
    internal async Task Disconnect(int player) => Accepted(await Engine.DisconnectAsync(Id, Players[player].Id, Connections[player]));
    internal Task<SessionSnapshot> Reconcile() => Store.UpdateAsync(Id, session => session.ReconcileTurns(Grace));

    internal static SessionSnapshot Accepted(GameEngineResult result)
    {
        Assert.True(result.Succeeded, result.Error?.Message);
        return Assert.IsType<SessionSnapshot>(result.State);
    }
}
