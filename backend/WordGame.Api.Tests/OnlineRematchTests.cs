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
using WordGame.Application.Matchmaking;
using WordGame.Application.Sessions;
using WordGame.Domain;

namespace WordGame.Api.Tests;

public sealed class OnlineRematchTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };

    [Fact]
    public async Task SimultaneousRequestsAndRetriesCreateOneFreshMatchAndNotifyThroughSourceRevision()
    {
        using var game = await Setup.Create();
        var before = (await game.Engine.GetStateAsync(game.Id)).State!;
        var replies = await Task.WhenAll(game.Respond(game.Alice, "request"), game.Respond(game.Bob, "request"));
        Assert.Contains(replies, reply => reply.Status == ApiRematchStatus.Pending);
        var ready = await game.Get(game.Alice);
        Assert.Equal(ApiRematchStatus.Ready, ready.Status);
        Assert.NotEqual(game.Id, ready.GameId);
        Assert.Equal(ready.GameId, (await game.Respond(game.Bob, "accept")).GameId);
        Assert.Equal(ready.GameId, (await game.Respond(game.Alice, "request")).GameId);
        Assert.Single(await game.Games.GetOpenAsync());
        var fresh = (await game.Engine.GetStateAsync(ready.GameId!.Value)).State!;
        Assert.Equal(SessionStatus.Active, fresh.Status);
        Assert.Equal(before.Game.Players, fresh.Game.Players);
        Assert.Equal(before.Game.Settings, fresh.Game.Settings);
        Assert.All(fresh.Game.Scores.Values, score => Assert.Equal(0, score.Points));
        Assert.Empty(fresh.Game.RoundResults);
        Assert.Null(fresh.Game.Result);
        Assert.Single(fresh.Game.Rounds);
        Assert.Equal(RoundStatus.CategorySelection, fresh.Game.CurrentRound!.Status);
        Assert.All(fresh.Players, player => Assert.False(player.HasLeft));
        var after = (await game.Engine.GetStateAsync(game.Id)).State!;
        Assert.True(after.Version > before.Version);
        Assert.Equal(before.Game.Result, after.Game.Result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task EitherPlayerCanRequestButOneAgreementCannotStart(bool first)
    {
        using var game = await Setup.Create();
        var caller = first ? game.Alice : game.Bob;
        var requested = await game.Respond(caller, "request");
        Assert.Equal(ApiRematchStatus.Pending, requested.Status);
        var version = (await game.Engine.GetStateAsync(game.Id)).State!.Version;
        Assert.Equal(ApiRematchStatus.Pending, (await game.Respond(caller, "request")).Status);
        Assert.Equal(ApiRematchStatus.Pending, (await game.Respond(caller, "accept")).Status);
        Assert.Equal(version, (await game.Engine.GetStateAsync(game.Id)).State!.Version);
        Assert.Equal(requested.ExpiresAt, (await game.Get(caller)).ExpiresAt);
        Assert.Empty(await game.Games.GetOpenAsync());
        Assert.Single((await game.Get(first ? game.Bob : game.Alice)).AcceptedPlayerIds);
    }

    [Fact]
    public async Task DeclineIsTerminalAndPlayerCanJoinTheQueue()
    {
        using var game = await Setup.Create();
        await game.Respond(game.Alice, "request");
        Assert.Equal(ApiRematchStatus.Declined, (await game.Respond(game.Bob, "decline")).Status);
        Assert.Equal(ApiRematchStatus.Declined, (await game.Respond(game.Alice, "accept")).Status);
        using var search = await game.Bob.PostAsJsonAsync("/api/v1/matchmaking/searches", new { displayName = "Bob" });
        search.EnsureSuccessStatusCode();
        Assert.Equal("searching", (await search.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        Assert.Empty(await game.Games.GetOpenAsync());
    }

    [Fact]
    public async Task DisconnectedOpponentCannotBlockPastDeadlineAndLateAcceptanceCannotReviveRequest()
    {
        using var game = await Setup.Create();
        await game.Respond(game.Alice, "request");
        game.Clock.Advance(TimeSpan.FromSeconds(29));
        Assert.Equal(ApiRematchStatus.Pending, (await game.Get(game.Bob)).Status);
        game.Clock.Advance(TimeSpan.FromSeconds(1));
        await game.Rematches.CleanupAsync();
        Assert.Equal(ApiRematchStatus.Expired, (await game.Get(game.Bob)).Status);
        Assert.Equal(ApiRematchStatus.Expired, (await game.Respond(game.Bob, "accept")).Status);
        Assert.Empty(await game.Games.GetOpenAsync());
    }

    [Fact]
    public async Task QueueEntryInvalidatesPendingNegotiation()
    {
        using var game = await Setup.Create();
        await game.Respond(game.Alice, "request");
        using var search = await game.Bob.PostAsJsonAsync("/api/v1/matchmaking/searches", new { displayName = "Bob" });
        search.EnsureSuccessStatusCode();
        Assert.Equal(ApiRematchStatus.Unavailable, (await game.Get(game.Alice)).Status);
        Assert.Equal(ApiRematchStatus.Unavailable, (await game.Respond(game.Bob, "accept")).Status);
    }

    [Fact]
    public async Task QueueEntryRacingFinalAcceptanceCannotAssignPlayersTwice()
    {
        using var game = await Setup.Create();
        await game.Respond(game.Alice, "request");
        var responses = await Task.WhenAll(
            game.Bob.PostAsJsonAsync(game.Path, new { action = "accept" }),
            game.Alice.PostAsJsonAsync("/api/v1/matchmaking/searches", new { displayName = "Alice" }));
        using var accept = responses[0]; using var search = responses[1];
        accept.EnsureSuccessStatusCode();
        var result = (await accept.Content.ReadFromJsonAsync<OnlineRematchResponse>(Json))!;
        var open = await game.Games.GetOpenAsync();
        if (result.Status == ApiRematchStatus.Ready)
        {
            Assert.Equal(HttpStatusCode.Conflict, search.StatusCode);
            Assert.Equal(result.GameId, Assert.Single(open).Id);
        }
        else
        {
            search.EnsureSuccessStatusCode();
            Assert.Equal(ApiRematchStatus.Unavailable, result.Status);
            Assert.Empty(open);
        }
    }

    [Fact]
    public async Task RevokedRequesterExpiresAndRetentionRemovesNegotiation()
    {
        using var game = await Setup.Create();
        await game.Respond(game.Alice, "request");
        using var reset = await game.Alice.PostAsJsonAsync("/api/v1/guests/reset", new { });
        reset.EnsureSuccessStatusCode();
        Assert.Equal(ApiRematchStatus.Expired, (await game.Get(game.Bob)).Status);
        game.Clock.Advance(TimeSpan.FromMinutes(11));
        await game.Games.CleanupAsync();
        Assert.Equal(0, await game.Rematches.CleanupAsync());
        using var missing = await game.Bob.GetAsync(game.Path);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task AuthorizationAndPhaseGuardsProtectRematchActions()
    {
        using var game = await Setup.Create();
        using var stranger = game.Factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var anonymous = await stranger.GetAsync(game.Path);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        await Setup.Guest(stranger);
        using var denied = await stranger.PostAsJsonAsync(game.Path, new { action = "request" });
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        using var invalid = await game.Alice.PostAsJsonAsync(game.Path, new { action = "accept" });
        Assert.Equal(HttpStatusCode.Conflict, invalid.StatusCode);
        await game.Respond(game.Alice, "request");
        var ready = await game.Respond(game.Bob, "accept");
        using var active = await game.Alice.PostAsJsonAsync($"/api/v1/games/{ready.GameId}/rematch", new { action = "request" });
        Assert.Equal(HttpStatusCode.Conflict, active.StatusCode);
    }

    private sealed class Setup : IDisposable
    {
        internal readonly WordGameApiFactory Root = new();
        internal WebApplicationFactory<Program> Factory = null!;
        internal HttpClient Alice = null!, Bob = null!;
        internal readonly ControlledGameplayClock Clock = new();
        internal GameEngine Engine => Factory.Services.GetRequiredService<GameEngine>();
        internal IGameSessionStore Games => Factory.Services.GetRequiredService<IGameSessionStore>();
        internal OnlineRematchService Rematches => Factory.Services.GetRequiredService<OnlineRematchService>();
        internal Guid Id;
        internal string Path => $"/api/v1/games/{Id}/rematch";
        internal static async Task<Setup> Create()
        {
            var setup = new Setup();
            setup.Factory = setup.Root.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services => services.AddSingleton<TimeProvider>(setup.Clock));
                builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Matchmaking:CleanupInterval"] = "01:00:00",
                    ["Sessions:CleanupInterval"] = "01:00:00",
                    ["Gameplay:TimeoutSweepInterval"] = "01:00:00"
                }));
            });
            setup.Alice = setup.Factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
            setup.Bob = setup.Factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
            var alice = await Guest(setup.Alice); var bob = await Guest(setup.Bob);
            var created = await setup.Engine.CreateAsync([new Player(alice, "Alice"), new Player(bob, "Bob")], MatchSettings.Online());
            setup.Id = created.State!.Id;
            Assert.True((await setup.Engine.InitializeAsync(setup.Id)).Succeeded);
            Assert.True((await setup.Engine.StartAsync(setup.Id)).Succeeded);
            var active = (await setup.Engine.GetStateAsync(setup.Id)).State!;
            Assert.True((await setup.Engine.ForfeitAsync(setup.Id, alice, new(Guid.NewGuid(), active.Version))).Succeeded);
            return setup;
        }
        internal async Task<OnlineRematchResponse> Get(HttpClient client) => (await client.GetFromJsonAsync<OnlineRematchResponse>(Path, Json))!;
        internal async Task<OnlineRematchResponse> Respond(HttpClient client, string action)
        {
            using var response = await client.PostAsJsonAsync(Path, new { action }); response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<OnlineRematchResponse>(Json))!;
        }
        internal static async Task<Guid> Guest(HttpClient client)
        {
            using var response = await client.PostAsJsonAsync("/api/v1/guests", new CreateGuestRequest()); response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<GuestIdentityResponse>())!.PlayerId;
        }
        public void Dispose() { Alice.Dispose(); Bob.Dispose(); Factory.Dispose(); Root.Dispose(); }
    }
}
