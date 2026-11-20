using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WordGame.Api.Contracts.V1;
using WordGame.Api.Tests.Fixtures;
using WordGame.Application.Games;
using WordGame.Application.Sessions;
using WordGame.Domain;

namespace WordGame.Api.Tests;

public sealed class OnlineDisconnectionTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };

    [Theory]
    [InlineData("forfeitMatch")]
    [InlineData("leaveMatch")]
    public async Task IntentionalDepartureAwardsTheOpponentAndNotifiesTheirStream(string type)
    {
        var clock = new ControlledGameplayClock();
        using var root = new WordGameApiFactory(); using var factory = Configure(root, clock);
        var a = await Guest(factory, "Alice"); var b = await Guest(factory, "Bob");
        using var alice = a.Client; using var bob = b.Client;
        var engine = factory.Services.GetRequiredService<GameEngine>();
        var game = await Start(engine, a.Player, b.Player);
        using var first = await Events(alice, game); using var second = await Events(bob, game);
        using var reader = new StreamReader(await second.Content.ReadAsStreamAsync());
        var version = (await State(bob, game)).Version;
        using var forfeit = await alice.PostAsJsonAsync(Path(game, "actions"), new { type, actionId = Guid.NewGuid(), expectedVersion = version });
        Assert.Equal(HttpStatusCode.OK, forfeit.StatusCode);
        await Notification(reader, "completed");
        var result = (await State(bob, game)).Result!;
        Assert.Equal(ApiMatchEndReason.Forfeit, result.Reason);
        Assert.Equal(a.Player.Id, result.ForfeitedPlayerId);
        Assert.Equal(b.Player.Id, Assert.Single(result.WinnerIds));
        var committed = (await engine.GetStateAsync(game)).State!;
        using var repeated = await alice.PostAsJsonAsync(Path(game, "actions"), new { type, actionId = Guid.NewGuid(), expectedVersion = (await State(alice, game)).Version });
        Assert.Equal(HttpStatusCode.Conflict, repeated.StatusCode);
        using var revisit = await Events(alice, game);
        var after = (await engine.GetStateAsync(game)).State!;
        Assert.True(after.Players[0].HasLeft);
        Assert.Equal(0, after.Players[0].ConnectionCount);
        Assert.Equal(committed.EndedAt, after.EndedAt);
        Assert.Same(committed.Game.Result, after.Game.Result);
    }

    [Fact]
    public async Task TemporaryDropAndMultipleTabsPreserveIdentityScoreRoundAndTurn()
    {
        var clock = new ControlledGameplayClock();
        using var root = new WordGameApiFactory(); using var factory = Configure(root, clock);
        var a = await Guest(factory, "Alice"); var b = await Guest(factory, "Bob");
        using var alice = a.Client; using var bob = b.Client;
        var engine = factory.Services.GetRequiredService<GameEngine>();
        var store = factory.Services.GetRequiredService<IGameSessionStore>();
        var game = await Start(engine, a.Player, b.Player);
        using var first = await Events(alice, game); using var extra = await Events(alice, game); using var other = await Events(bob, game);
        async Task<PlayerActionContext> Context() => new(Guid.NewGuid(), (await store.GetAsync(game))!.Version);
        Assert.True((await engine.SelectCategoryAsync(game, a.Player.Id, "european-capitals", await Context())).Succeeded);
        Assert.True((await engine.PlaceBidAsync(game, a.Player.Id, 1, await Context())).Succeeded);
        Assert.True((await engine.CallBluffAsync(game, b.Player.Id, await Context())).Succeeded);
        Assert.True((await engine.SubmitWordAsync(game, a.Player.Id, "Stockholm", await Context())).Succeeded);
        Assert.True((await engine.NextRoundAsync(game)).Succeeded);
        first.Dispose();
        await WaitFor(async () => (await store.GetAsync(game))!.Players[0].ConnectionCount == 1);
        var before = await State(bob, game);
        Assert.True(before.Players[0].Score > 0);
        extra.Dispose();
        await WaitFor(async () => (await store.GetAsync(game))!.Players[0].ConnectionCount == 0);
        var disconnected = (await State(bob, game)).Players[0];
        Assert.False(disconnected.HasLeft);
        Assert.Equal(clock.GetUtcNow().AddSeconds(5), disconnected.ReconnectUntil);
        clock.Advance(TimeSpan.FromSeconds(4));
        using var reconnect = await Events(alice, game);
        var restored = await State(alice, game);
        Assert.Equal(ApiSessionStatus.Active, restored.Status);
        Assert.Equal(before.Players[0].Id, restored.Players[0].Id);
        Assert.Equal(before.Players[0].Score, restored.Players[0].Score);
        Assert.Equal(before.Round!.Id, restored.Round!.Id);
        Assert.Equal(before.Round.ActivePlayerId, restored.Round.ActivePlayerId);
        Assert.True(restored.Players[0].IsConnected);
    }

    [Fact]
    public async Task ExpiredGraceAutomaticallyDeliversTheOutcomeWithoutClientPolling()
    {
        var clock = new ControlledGameplayClock();
        using var root = new WordGameApiFactory(); using var factory = Configure(root, clock, true);
        var a = await Guest(factory, "Alice"); var b = await Guest(factory, "Bob");
        using var alice = a.Client; using var bob = b.Client;
        var engine = factory.Services.GetRequiredService<GameEngine>();
        var store = factory.Services.GetRequiredService<IGameSessionStore>();
        var game = await Start(engine, a.Player, b.Player);
        using var first = await Events(alice, game); using var other = await Events(bob, game);
        using var reader = new StreamReader(await other.Content.ReadAsStreamAsync());
        first.Dispose();
        await WaitFor(async () => (await store.GetAsync(game))!.Players[0].ConnectionCount == 0);
        clock.Advance(TimeSpan.FromSeconds(5));
        await Notification(reader, "completed");
        var final = await State(bob, game);
        Assert.Equal(ApiMatchEndReason.ReconnectionExpired, final.Result!.Reason);
        Assert.Equal(b.Player.Id, Assert.Single(final.Result.WinnerIds));
        using var late = await Events(alice, game);
        Assert.Equal(0, (await store.GetAsync(game))!.Players[0].ConnectionCount);
    }

    [Fact]
    public async Task ConcurrentForfeitsAndExpiredWindowRacesCannotChangeAFinalOutcome()
    {
        var clock = new ControlledGameplayClock();
        using var root = new WordGameApiFactory(); using var factory = Configure(root, clock);
        var a = await Guest(factory, "Alice"); var b = await Guest(factory, "Bob");
        using var alice = a.Client; using var bob = b.Client;
        var engine = factory.Services.GetRequiredService<GameEngine>();
        var store = factory.Services.GetRequiredService<IGameSessionStore>();
        var game = await Start(engine, a.Player, b.Player);
        using var first = await Events(alice, game); using var second = await Events(bob, game);
        var version = (await State(alice, game)).Version;
        var responses = await Task.WhenAll(alice.PostAsJsonAsync(Path(game, "actions"), new { type = "forfeitMatch", actionId = Guid.NewGuid(), expectedVersion = version }),
            bob.PostAsJsonAsync(Path(game, "actions"), new { type = "forfeitMatch", actionId = Guid.NewGuid(), expectedVersion = version }));
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        }
        finally { foreach (var response in responses) { response.Dispose(); } }
        var committed = (await store.GetAsync(game))!;
        clock.Advance(TimeSpan.FromSeconds(30));
        await Task.WhenAll(store.CleanupAsync(), store.CleanupAsync());
        var after = (await store.GetAsync(game))!;
        Assert.Equal(committed.EndedAt, after.EndedAt);
        Assert.Same(committed.Game.Result, after.Game.Result);
    }

    [Fact]
    public async Task BothDisconnectedMatchesAreAbandonedAndEventuallyRemoved()
    {
        var clock = new ControlledGameplayClock();
        using var root = new WordGameApiFactory(); using var factory = Configure(root, clock);
        var a = await Guest(factory, "Alice"); var b = await Guest(factory, "Bob");
        using var alice = a.Client; using var bob = b.Client;
        var store = factory.Services.GetRequiredService<IGameSessionStore>();
        var game = await Start(factory.Services.GetRequiredService<GameEngine>(), a.Player, b.Player);
        using var first = await Events(alice, game); using var second = await Events(bob, game);
        first.Dispose(); second.Dispose();
        await WaitFor(async () => (await store.GetAsync(game))!.Players.All(player => player.ConnectionCount == 0));
        clock.Advance(TimeSpan.FromSeconds(5));
        await store.CleanupAsync();
        Assert.Equal(SessionStatus.Abandoned, (await store.GetAsync(game))!.Status);
        Assert.Null((await store.GetAsync(game))!.Game.Result);
        clock.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal(1, await store.CleanupAsync());
        Assert.Null(await store.GetAsync(game));
    }

    [Fact]
    public async Task TimeoutWinsOverACommandReceivedAfterTheReconnectionDeadline()
    {
        var clock = new ControlledGameplayClock();
        using var root = new WordGameApiFactory(); using var factory = Configure(root, clock);
        var a = await Guest(factory, "Alice"); var b = await Guest(factory, "Bob");
        using var alice = a.Client; using var bob = b.Client;
        var store = factory.Services.GetRequiredService<IGameSessionStore>();
        var game = await Start(factory.Services.GetRequiredService<GameEngine>(), a.Player, b.Player);
        using var first = await Events(alice, game); using var second = await Events(bob, game);
        first.Dispose();
        await WaitFor(async () => (await store.GetAsync(game))!.Players[0].ConnectionCount == 0);
        var version = (await State(bob, game)).Version;
        clock.Advance(TimeSpan.FromSeconds(5));
        var command = bob.PostAsJsonAsync(Path(game, "actions"), new { type = "forfeitMatch", actionId = Guid.NewGuid(), expectedVersion = version });
        var cleanup = store.CleanupAsync();
        using var response = await command;
        await cleanup;
        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.Forbidden, HttpStatusCode.Conflict });
        var result = (await State(bob, game)).Result!;
        Assert.Equal(ApiMatchEndReason.ReconnectionExpired, result.Reason);
        Assert.Equal(b.Player.Id, Assert.Single(result.WinnerIds));
    }

    [Fact]
    public async Task AcceptanceWaitingTimeDoesNotConsumeTheActiveReconnectionWindow()
    {
        var clock = new ControlledGameplayClock();
        using var root = new WordGameApiFactory(); using var factory = Configure(root, clock);
        var a = await Guest(factory, "Alice"); var b = await Guest(factory, "Bob");
        using var alice = a.Client; using var bob = b.Client;
        var engine = factory.Services.GetRequiredService<GameEngine>();
        var created = await engine.CreateAsync([a.Player, b.Player], MatchSettings.Online());
        var game = created.State!.Id;
        Assert.True((await engine.InitializeAsync(game)).Succeeded);
        clock.Advance(TimeSpan.FromSeconds(30));
        await factory.Services.GetRequiredService<IGameSessionStore>().CleanupAsync();
        Assert.True((await engine.StartAsync(game)).Succeeded);
        using var first = await Events(alice, game);
        var state = await State(alice, game);
        Assert.Equal(ApiSessionStatus.Active, state.Status);
        Assert.Equal(clock.GetUtcNow().AddSeconds(5), state.Players[1].ReconnectUntil);
    }

    private static WebApplicationFactory<Program> Configure(WordGameApiFactory root, TimeProvider clock, bool worker = false) =>
        root.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services => services.AddSingleton(clock));
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Sessions:OnlineDisconnectedGrace"] = "00:00:05",
                ["Sessions:DisconnectedGrace"] = "00:00:00",
                ["Sessions:CleanupInterval"] = "01:00:00",
                ["Gameplay:TimeoutSweepInterval"] = worker ? "00:00:00.020" : "01:00:00"
            }));
        });
    private static async Task<(HttpClient Client, Player Player)> Guest(WebApplicationFactory<Program> factory, string name)
    {
        var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var response = await client.PostAsJsonAsync("/api/v1/guests", new CreateGuestRequest());
        response.EnsureSuccessStatusCode();
        return (client, new Player((await response.Content.ReadFromJsonAsync<GuestIdentityResponse>())!.PlayerId, name));
    }
    private static async Task<Guid> Start(GameEngine engine, params Player[] players)
    {
        var created = await engine.CreateAsync(players, MatchSettings.Online());
        Assert.True(created.Succeeded);
        Assert.True((await engine.InitializeAsync(created.State!.Id)).Succeeded);
        Assert.True((await engine.StartAsync(created.State.Id)).Succeeded);
        return created.State.Id;
    }
    private static string Path(Guid game, string suffix) => $"/api/v1/games/{game}/{suffix}";
    private static async Task<GameStateResponse> State(HttpClient client, Guid game) =>
        (await client.GetFromJsonAsync<GameStateResponse>(Path(game, "state"), Json))!;
    private static async Task<HttpResponseMessage> Events(HttpClient client, Guid game)
    {
        var response = await client.GetAsync(Path(game, "events"), HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode(); return response;
    }
    private static async Task Notification(StreamReader reader, string status)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (await reader.ReadLineAsync(timeout.Token) is { } line)
        {
            if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                using var payload = JsonDocument.Parse(line[5..]);
                if (payload.RootElement.GetProperty("status").GetString() == status) { return; }
            }
        }
        Assert.Fail("The stream ended without the expected outcome.");
    }
    private static async Task WaitFor(Func<Task<bool>> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!await condition()) { await Task.Delay(20, timeout.Token); }
    }
}
