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

public sealed class PartyReconnectionTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };

    [Fact]
    public async Task RealGuestStreamsRestoreIdentityScoreAndRoundAndDistinguishIntentionalLeave()
    {
        var clock = new ControlledGameplayClock();
        using var root = new WordGameApiFactory(); using var factory = Configure(root, clock);
        var host = await Guest(factory, "Host"); var bob = await Guest(factory, "Bob");
        using var hostClient = host.Client; using var bobClient = bob.Client;
        var engine = factory.Services.GetRequiredService<GameEngine>();
        var store = factory.Services.GetRequiredService<IGameSessionStore>();
        var game = (await engine.CreateAsync([host.Player, bob.Player], MatchSettings.Party(4))).State!.Id;
        await engine.InitializeAsync(game); await engine.StartAsync(game);
        using var original = await Events(hostClient, game); using var other = await Events(bobClient, game);
        async Task<PlayerActionContext> Context() => new(Guid.NewGuid(), (await store.GetAsync(game))!.Version);
        Assert.True((await engine.SelectCategoryAsync(game, host.Player.Id, "european-capitals", await Context())).Succeeded);
        Assert.True((await engine.PlaceBidAsync(game, host.Player.Id, 1, await Context())).Succeeded);
        Assert.True((await engine.CallBluffAsync(game, bob.Player.Id, await Context())).Succeeded);
        Assert.True((await engine.SubmitWordAsync(game, host.Player.Id, "Stockholm", await Context())).Succeeded);
        Assert.True((await engine.NextRoundAsync(game)).Succeeded);
        var before = await State(hostClient, game);
        var score = before.Players.Single(player => player.Id == host.Player.Id).Score;
        Assert.True(score > 0);
        original.Dispose();
        await WaitFor(async () => (await store.GetAsync(game))!.Players[0].ConnectionCount == 0);
        var offline = (await State(bobClient, game)).Players[0];
        Assert.False(offline.IsConnected); Assert.False(offline.HasLeft);
        Assert.Equal(clock.GetUtcNow().AddMinutes(2), offline.ReconnectUntil);
        clock.Advance(TimeSpan.FromSeconds(5));
        using var restored = await Events(hostClient, game); using var anotherTab = await Events(hostClient, game);
        var after = await State(hostClient, game);
        Assert.Equal(before.Round!.Id, after.Round!.Id);
        Assert.Equal(before.Round.ActivePlayerId, after.Round.ActivePlayerId);
        Assert.Equal(score, after.Players[0].Score);
        Assert.Equal(host.Player.Id, after.Players[0].Id);
        Assert.Equal(2, after.Players.Count);
        Assert.Null(after.Players[0].ReconnectUntil);
        restored.Dispose();
        await WaitFor(async () => (await store.GetAsync(game))!.Players[0].ConnectionCount == 1);
        Assert.True((await State(bobClient, game)).Players[0].IsConnected);
        after = await State(hostClient, game);
        using var leave = await hostClient.PostAsJsonAsync($"/api/v1/games/{game}/actions", new { type = "leaveMatch", actionId = Guid.NewGuid(), expectedVersion = after.Version });
        leave.EnsureSuccessStatusCode();
        var departed = await State(bobClient, game);
        Assert.True(departed.Players[0].HasLeft);
        Assert.False(departed.Players[0].IsConnected);
        Assert.Null(departed.Players[0].ReconnectUntil);
        using var revisit = await Events(hostClient, game);
        Assert.Equal(0, (await store.GetAsync(game))!.Players[0].ConnectionCount);
        Assert.Equal(score, (await State(hostClient, game)).Players[0].Score);
    }

    [Fact]
    public async Task ExpiredGraceIsEnforcedWhenAStreamReconnectsEvenBeforeTheCleanupSweep()
    {
        var clock = new ControlledGameplayClock();
        using var root = new WordGameApiFactory(); using var factory = Configure(root, clock);
        var host = await Guest(factory, "Host"); var bob = await Guest(factory, "Bob");
        using var hostClient = host.Client; using var bobClient = bob.Client;
        var engine = factory.Services.GetRequiredService<GameEngine>();
        var store = factory.Services.GetRequiredService<IGameSessionStore>();
        var game = (await engine.CreateAsync([host.Player, bob.Player])).State!.Id;
        await engine.InitializeAsync(game); await engine.StartAsync(game);
        using var stream = await Events(hostClient, game); using var peer = await Events(bobClient, game);
        stream.Dispose();
        await WaitFor(async () => (await store.GetAsync(game))!.Players[0].ConnectionCount == 0);
        clock.Advance(TimeSpan.FromMinutes(2));
        using var late = await Events(hostClient, game);
        Assert.Equal(ApiSessionStatus.Abandoned, (await State(hostClient, game)).Status);
        Assert.Equal(0, (await store.GetAsync(game))!.Players[0].ConnectionCount);
    }

    private static WebApplicationFactory<Program> Configure(WordGameApiFactory root, ControlledGameplayClock clock) => root.WithWebHostBuilder(builder =>
    {
        builder.ConfigureServices(services => services.AddSingleton<TimeProvider>(clock));
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        { ["Gameplay:TimeoutSweepInterval"] = "01:00:00", ["Sessions:CleanupInterval"] = "01:00:00" }));
    });
    private static async Task<(HttpClient Client, Player Player)> Guest(WebApplicationFactory<Program> factory, string name)
    {
        var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var response = await client.PostAsJsonAsync("/api/v1/guests", new CreateGuestRequest());
        response.EnsureSuccessStatusCode();
        return (client, new Player((await response.Content.ReadFromJsonAsync<GuestIdentityResponse>())!.PlayerId, name));
    }
    private static async Task<GameStateResponse> State(HttpClient client, Guid game) => (await client.GetFromJsonAsync<GameStateResponse>($"/api/v1/games/{game}/state", Json))!;
    private static async Task<HttpResponseMessage> Events(HttpClient client, Guid game)
    {
        var response = await client.GetAsync($"/api/v1/games/{game}/events", HttpCompletionOption.ResponseHeadersRead); response.EnsureSuccessStatusCode(); return response;
    }
    private static async Task WaitFor(Func<Task<bool>> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!await condition())
        {
            await Task.Delay(20, timeout.Token);
        }
    }
}
