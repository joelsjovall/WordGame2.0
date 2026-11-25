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
using WordGame.Application.Lobbies;
using WordGame.Application.Sessions;

namespace WordGame.Api.Tests;

public sealed class PartyLifecycleTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };

    [Fact]
    public async Task PublicActionsCompleteAMatchRetainResultsAndStartAFreshRematchInTheSameLobby()
    {
        var clock = new ControlledGameplayClock();
        using var root = new WordGameApiFactory();
        using var factory = root.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services => services.AddSingleton<TimeProvider>(clock));
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            { ["Sessions:LobbyStartCountdown"] = "00:00:00", ["Gameplay:TimeoutSweepInterval"] = "00:00:00.020", ["Sessions:CleanupInterval"] = "01:00:00" }));
        });
        using var host = await Guest(factory); using var bob = await Guest(factory); using var stranger = await Guest(factory);
        var room = await Create(host); await Join(bob, room);
        using var hostLobby = await Events(host, $"/api/v1/lobbies/{room.Id}/events");
        using var bobLobby = await Events(bob, $"/api/v1/lobbies/{room.Id}/events");
        var current = await Room(host, room.Id);
        using var settings = await host.PostAsJsonAsync($"/api/v1/lobbies/{room.Id}/settings", new
        {
            expectedVersion = current.Version,
            settings = new { maximumPlayers = 2, roundCount = 2, challengeSeconds = 5, allowedDifficulties = new[] { "easy" } }
        });
        settings.EnsureSuccessStatusCode();
        await Ready(host, room.Id); await Ready(bob, room.Id);
        current = await Room(host, room.Id);
        using var first = await host.PostAsJsonAsync($"/api/v1/lobbies/{room.Id}/start", new StartLobbyRequest(current.Version));
        first.EnsureSuccessStatusCode();
        var active = (await first.Content.ReadFromJsonAsync<LobbyResponse>(Json))!;
        var gameId = active.GameId!.Value;
        using var spectator = factory.CreateClient();
        var watched = await spectator.GetFromJsonAsync<SpectatorStateResponse>(
            $"/api/v1/lobbies/{room.Id}/spectate/{room.LobbyCode}", Json);
        Assert.Equal(gameId, watched!.Game!.Id);
        using var hostGame = await Events(host, $"/api/v1/games/{gameId}/events");
        using var bobGame = await Events(bob, $"/api/v1/games/{gameId}/events");
        using var forbidden = await stranger.PostAsJsonAsync($"/api/v1/games/{gameId}/actions", new { type = "placeBid", actionId = Guid.NewGuid(), expectedVersion = "0", wordCount = 1 });
        Assert.Equal(HttpStatusCode.NotFound, forbidden.StatusCode);
        using var anonymous = factory.CreateClient();
        using var unauthorized = await anonymous.PostAsJsonAsync($"/api/v1/games/{gameId}/actions", new { type = "callBluff", actionId = Guid.NewGuid(), expectedVersion = "0" });
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        using var spoofed = await host.PostAsJsonAsync($"/api/v1/games/{gameId}/actions", new { type = "placeBid", actionId = Guid.NewGuid(), expectedVersion = "0", wordCount = 1, playerId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.BadRequest, spoofed.StatusCode);
        using var staleAction = await host.PostAsJsonAsync($"/api/v1/games/{gameId}/actions", new { type = "placeBid", actionId = Guid.NewGuid(), expectedVersion = "0", wordCount = 1 });
        Assert.Equal(HttpStatusCode.Conflict, staleAction.StatusCode);
        using var foreignRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/games/{gameId}/actions")
        { Content = JsonContent.Create(new { type = "callBluff", actionId = Guid.NewGuid(), expectedVersion = "0" }) };
        foreignRequest.Headers.Add("Origin", "https://foreign.example");
        using var foreign = await host.SendAsync(foreignRequest);
        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
        var lifecycle = factory.Services.GetRequiredService<PartyLifecycleService>();
        for (var round = 1; round <= 2; round++)
        {
            var state = await State(host, gameId);
            var bidder = state.Round!.ActivePlayerId == room.HostPlayerId ? host : bob;
            var caller = ReferenceEquals(bidder, host) ? bob : host;
            await Action(bidder, gameId, "selectCategory");
            await Action(bidder, gameId, "placeBid");
            await Action(caller, gameId, "callBluff");
            await Action(bidder, gameId, "submitWord");
            if (round == 1)
            {
                clock.Advance(TimeSpan.FromSeconds(3));
                await WaitForRound(host, gameId, 2);
            }

            await lifecycle.RefreshAsync((await factory.Services.GetRequiredService<ILobbySessionStore>().GetAsync(room.Id))!);
        }
        var completed = await Room(host, room.Id);
        Assert.Equal(ApiLobbyStatus.Completed, completed.Status);
        Assert.NotNull(completed.LastResult);
        Assert.Equal(2, completed.LastResult.CompletedRoundCount);
        Assert.All(completed.LastResult.Standings, standing => Assert.True(standing.Score > 0));
        var scores = completed.LastResult.Standings.Select(standing => standing.Score).ToArray();
        clock.Advance(TimeSpan.FromMinutes(11));
        await factory.Services.GetRequiredService<IGameSessionStore>().CleanupAsync();
        Assert.Null(await factory.Services.GetRequiredService<IGameSessionStore>().GetAsync(gameId));
        Assert.Equal(scores, (await Room(host, room.Id)).LastResult!.Standings.Select(standing => standing.Score));
        using var deniedRematch = await bob.PostAsJsonAsync($"/api/v1/lobbies/{room.Id}/rematch", new StartLobbyRequest(completed.Version));
        Assert.Equal(HttpStatusCode.Forbidden, deniedRematch.StatusCode);
        using var rematch = await host.PostAsJsonAsync($"/api/v1/lobbies/{room.Id}/rematch", new StartLobbyRequest(completed.Version));
        rematch.EnsureSuccessStatusCode();
        var waiting = await Room(host, room.Id);
        Assert.Equal(ApiLobbyStatus.Waiting, waiting.Status);
        Assert.Null(waiting.GameId);
        Assert.All(waiting.Players, player => Assert.False(player.IsReady));
        Assert.Equal(JsonSerializer.Serialize(completed.Settings, Json), JsonSerializer.Serialize(waiting.Settings, Json));
        await Ready(host, room.Id); await Ready(bob, room.Id);
        waiting = await Room(host, room.Id);
        using var second = await host.PostAsJsonAsync($"/api/v1/lobbies/{room.Id}/start", new StartLobbyRequest(waiting.Version));
        second.EnsureSuccessStatusCode();
        var restarted = (await second.Content.ReadFromJsonAsync<LobbyResponse>(Json))!;
        Assert.Equal(room.Id, restarted.Id);
        Assert.Equal(room.LobbyCode, restarted.LobbyCode);
        Assert.NotEqual(gameId, restarted.GameId);
        Assert.Null(restarted.LastResult);
        var fresh = await State(host, restarted.GameId!.Value);
        Assert.All(fresh.Players, player => Assert.Equal(0, player.Score));
        Assert.Null(fresh.Result);
        Assert.Null(fresh.Round!.HighestBid); Assert.Null(fresh.Round.Challenge); Assert.Null(fresh.Round.Result);
        Assert.Equal(1, fresh.Round.Number);
        using var retry = await host.PostAsJsonAsync($"/api/v1/lobbies/{room.Id}/start", new StartLobbyRequest(waiting.Version));
        Assert.Equal(restarted.GameId, (await retry.Content.ReadFromJsonAsync<LobbyResponse>(Json))!.GameId);
    }

    [Fact]
    public async Task EveryMemberSeesTheSameCountdownAndConcurrentStartsDoNotCreateTwoMatches()
    {
        using var root = new WordGameApiFactory();
        using var factory = root.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
            new Dictionary<string, string?> { ["Sessions:LobbyStartCountdown"] = "00:00:01" })));
        using var host = await Guest(factory); using var bob = await Guest(factory);
        var room = await Create(host); await Join(bob, room);
        using var first = await Events(host, $"/api/v1/lobbies/{room.Id}/events");
        using var second = await Events(bob, $"/api/v1/lobbies/{room.Id}/events");
        await Ready(host, room.Id); await Ready(bob, room.Id);
        var ready = await Room(host, room.Id);
        var pending = host.PostAsJsonAsync($"/api/v1/lobbies/{room.Id}/start", new StartLobbyRequest(ready.Version));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var a = await EventState(first, ApiLobbyStatus.Starting, timeout.Token); var b = await EventState(second, ApiLobbyStatus.Starting, timeout.Token);
        Assert.Equal(a.StartsAt, b.StartsAt);
        Assert.NotNull(a.StartsAt);
        Assert.False(pending.IsCompleted);
        using var duplicate = await host.PostAsJsonAsync($"/api/v1/lobbies/{room.Id}/start", new StartLobbyRequest(ready.Version));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        using var started = await pending;
        var active = (await started.Content.ReadFromJsonAsync<LobbyResponse>(Json))!;
        Assert.True(active.ServerTime >= active.StartsAt);
        var firstStarted = await EventState(first, ApiLobbyStatus.Started, timeout.Token);
        var secondStarted = await EventState(second, ApiLobbyStatus.Started, timeout.Token);
        Assert.Equal(active.GameId, firstStarted.GameId);
        Assert.Equal(firstStarted.GameId, secondStarted.GameId);
        Assert.Equal(firstStarted.StartsAt, secondStarted.StartsAt);
        Assert.Single(await factory.Services.GetRequiredService<IGameSessionStore>().GetActiveAsync());
    }

    [Fact]
    public async Task AbandonedMatchReturnsToResultsAndEmptyLobbyAndSessionAreCleanedUp()
    {
        var clock = new ControlledGameplayClock();
        using var root = new WordGameApiFactory();
        using var factory = root.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services => services.AddSingleton<TimeProvider>(clock));
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["Sessions:LobbyStartCountdown"] = "00:00:00" }));
        });
        using var host = await Guest(factory); using var bob = await Guest(factory);
        var room = await Create(host); await Join(bob, room);
        using var hostEvents = await Events(host, $"/api/v1/lobbies/{room.Id}/events");
        using var bobEvents = await Events(bob, $"/api/v1/lobbies/{room.Id}/events");
        await Ready(host, room.Id); await Ready(bob, room.Id);
        var ready = await Room(host, room.Id);
        using var response = await host.PostAsJsonAsync($"/api/v1/lobbies/{room.Id}/start", new StartLobbyRequest(ready.Version));
        response.EnsureSuccessStatusCode();
        var active = (await response.Content.ReadFromJsonAsync<LobbyResponse>(Json))!;
        await factory.Services.GetRequiredService<WordGame.Application.Games.GameEngine>().CancelAsync(active.GameId!.Value);
        await factory.Services.GetRequiredService<PartyLifecycleService>().RefreshAsync((await factory.Services.GetRequiredService<ILobbySessionStore>().GetAsync(room.Id))!);
        var ended = await Room(host, room.Id);
        Assert.Equal(ApiLobbyStatus.Completed, ended.Status);
        Assert.Null(ended.LastResult);
        using var hostLeave = await host.PostAsJsonAsync($"/api/v1/lobbies/{room.Id}/leave", new LeaveLobbyRequest());
        hostLeave.EnsureSuccessStatusCode();
        using var bobLeave = await bob.PostAsJsonAsync($"/api/v1/lobbies/{room.Id}/leave", new LeaveLobbyRequest());
        bobLeave.EnsureSuccessStatusCode();
        Assert.Null(await factory.Services.GetRequiredService<ILobbySessionStore>().GetAsync(room.Id));
        clock.Advance(TimeSpan.FromMinutes(6));
        await factory.Services.GetRequiredService<IGameSessionStore>().CleanupAsync();
        Assert.Null(await factory.Services.GetRequiredService<IGameSessionStore>().GetAsync(active.GameId.Value));
    }

    [Fact]
    public async Task FourPlayersEachTakeATurnAndCompletedResultsKeepHostPermissionsForRematches()
    {
        var clock = new ControlledGameplayClock();
        using var root = new WordGameApiFactory();
        using var factory = root.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services => services.AddSingleton<TimeProvider>(clock));
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            { ["Sessions:LobbyStartCountdown"] = "00:00:00", ["Gameplay:TimeoutSweepInterval"] = "00:00:00.020", ["Sessions:CleanupInterval"] = "01:00:00" }));
        });
        using var host = await Guest(factory);
        using var bob = await Guest(factory);
        using var carol = await Guest(factory);
        using var dan = await Guest(factory);
        var clients = new[] { host, bob, carol, dan };
        var room = await Create(host);
        for (var index = 1; index < clients.Length; index++)
        {
            using var join = await clients[index].PostAsJsonAsync("/api/v1/lobbies/join", new JoinLobbyRequest(room.LobbyCode, $"Player {index}"));
            join.EnsureSuccessStatusCode();
        }
        var streams = new List<HttpResponseMessage>();
        try
        {
            foreach (var client in clients)
            {
                streams.Add(await Events(client, $"/api/v1/lobbies/{room.Id}/events"));
            }
            var current = await Room(host, room.Id);
            using var settings = await host.PostAsJsonAsync($"/api/v1/lobbies/{room.Id}/settings", new
            {
                expectedVersion = current.Version,
                settings = new { maximumPlayers = 4, roundCount = 4, challengeSeconds = 5, allowedDifficulties = new[] { "easy" } }
            });
            settings.EnsureSuccessStatusCode();
            foreach (var client in clients)
            {
                await Ready(client, room.Id);
            }
            current = await Room(host, room.Id);
            using var start = await host.PostAsJsonAsync($"/api/v1/lobbies/{room.Id}/start", new StartLobbyRequest(current.Version));
            start.EnsureSuccessStatusCode();
            var gameId = (await Room(host, room.Id)).GameId!.Value;
            foreach (var client in clients)
            {
                streams.Add(await Events(client, $"/api/v1/games/{gameId}/events"));
            }
            var playerIds = current.Players.Select(player => player.Id).ToArray();
            var starters = new HashSet<Guid>();
            var lifecycle = factory.Services.GetRequiredService<PartyLifecycleService>();
            for (var round = 1; round <= 4; round++)
            {
                var state = await State(host, gameId);
                Assert.Equal(round, state.Round!.Number);
                var active = state.Round.ActivePlayerId;
                Assert.True(starters.Add(active));
                var bidderIndex = Array.IndexOf(playerIds, active);
                var bidder = clients[bidderIndex];
                var caller = clients[(bidderIndex + 1) % clients.Length];
                await Action(bidder, gameId, "selectCategory");
                await Action(bidder, gameId, "placeBid");
                await Action(caller, gameId, "callBluff");
                await Action(bidder, gameId, "submitWord");
                clock.Advance(TimeSpan.FromSeconds(3));
                if (round < 4) { await WaitForRound(host, gameId, round + 1); }
                await lifecycle.RefreshAsync((await factory.Services.GetRequiredService<ILobbySessionStore>().GetAsync(room.Id))!);
            }
            var completed = await Room(host, room.Id);
            Assert.Equal(ApiLobbyStatus.Completed, completed.Status);
            Assert.Equal(4, completed.LastResult!.CompletedRoundCount);
            Assert.Equal(4, completed.LastResult.Standings.Count);
            Assert.All(completed.LastResult.Standings, standing => Assert.True(standing.Score > 0));
            using var forbidden = await dan.PostAsJsonAsync($"/api/v1/lobbies/{room.Id}/rematch", new StartLobbyRequest(completed.Version));
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
            using var rematch = await host.PostAsJsonAsync($"/api/v1/lobbies/{room.Id}/rematch", new StartLobbyRequest(completed.Version));
            rematch.EnsureSuccessStatusCode();
            var waiting = await Room(host, room.Id);
            Assert.Equal(room.LobbyCode, waiting.LobbyCode);
            Assert.Equal(ApiLobbyStatus.Waiting, waiting.Status);
            Assert.Equal(4, waiting.Players.Count);
            Assert.All(waiting.Players, player => Assert.False(player.IsReady));
        }
        finally
        {
            foreach (var stream in streams)
            {
                stream.Dispose();
            }
        }
    }

    private static async Task<LobbyResponse> EventState(HttpResponseMessage response, ApiLobbyStatus status, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(cancellationToken), leaveOpen: true);
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                var state = JsonSerializer.Deserialize<LobbyResponse>(line[5..], Json)!;
                if (state.Status == status)
                {
                    return state;
                }
            }
        }

        throw new InvalidOperationException("No countdown received.");
    }
    private static async Task WaitForRound(HttpClient client, Guid game, int round)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while ((await State(client, game)).Round!.Number != round) { await Task.Delay(20, timeout.Token); }
    }
    private static async Task Action(HttpClient client, Guid game, string type)
    {
        var state = await State(client, game);
        var body = new Dictionary<string, object> { ["type"] = type, ["actionId"] = Guid.NewGuid(), ["expectedVersion"] = state.Version };
        if (type == "selectCategory")
        {
            body["categoryId"] = "european-capitals";
        }

        if (type == "placeBid")
        {
            body["wordCount"] = 1;
        }

        if (type == "submitWord")
        {
            body["word"] = "Stockholm";
        }

        using var response = await client.PostAsJsonAsync($"/api/v1/games/{game}/actions", body);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }
    private static async Task<HttpClient> Guest(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var response = await client.PostAsJsonAsync("/api/v1/guests", new CreateGuestRequest()); response.EnsureSuccessStatusCode(); return client;
    }
    private static async Task<LobbyResponse> Create(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/lobbies", new CreateLobbyRequest(Guid.NewGuid(), "Host"));
        return (await response.Content.ReadFromJsonAsync<LobbyResponse>(Json))!;
    }
    private static async Task Join(HttpClient client, LobbyResponse room)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/lobbies/join", new JoinLobbyRequest(room.LobbyCode, "Bob")); response.EnsureSuccessStatusCode();
    }
    private static async Task<LobbyResponse> Room(HttpClient client, Guid id) => (await client.GetFromJsonAsync<LobbyResponse>($"/api/v1/lobbies/{id}", Json))!;
    private static async Task<GameStateResponse> State(HttpClient client, Guid id) => (await client.GetFromJsonAsync<GameStateResponse>($"/api/v1/games/{id}/state", Json))!;
    private static async Task Ready(HttpClient client, Guid id)
    {
        var room = await Room(client, id); using var response = await client.PostAsJsonAsync($"/api/v1/lobbies/{id}/ready", new SetLobbyReadyRequest(room.Version, true)); response.EnsureSuccessStatusCode();
    }
    private static async Task<HttpResponseMessage> Events(HttpClient client, string url)
    {
        var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead); response.EnsureSuccessStatusCode(); return response;
    }
}
