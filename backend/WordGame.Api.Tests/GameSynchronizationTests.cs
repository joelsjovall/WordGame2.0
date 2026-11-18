using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WordGame.Api.Tests.Fixtures;
using WordGame.Application.Games;
using WordGame.Application.Sessions;
using WordGame.Domain;

namespace WordGame.Api.Tests;

public sealed class GameSynchronizationTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public async Task EveryParticipantReceivesEngineChangesAndCanRetrieveAuthoritativeState(int count)
    {
        using var factory = new WordGameApiFactory();
        var engine = factory.Services.GetRequiredService<GameEngine>();
        var players = Enumerable.Range(0, count).Select(index => new Player(Guid.NewGuid(), $"Player {index}")).ToArray();
        var created = await engine.CreateAsync(players, MatchSettings.Party(1));
        var id = created.State!.Id;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var clients = players.Select(player => AuthenticatedPlayerClient.Create(factory, player.Id)).ToArray();
        var responses = new List<HttpResponseMessage>();
        var readers = new List<StreamReader>();
        try
        {
            foreach (var client in clients)
            {
                var response = await client.GetAsync($"/api/v1/games/{id}/events", HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Equal("text/event-stream", response.Content.Headers.ContentType!.MediaType);
                Assert.True(response.Headers.CacheControl!.NoStore);
                responses.Add(response);
                readers.Add(new StreamReader(await response.Content.ReadAsStreamAsync(timeout.Token)));
            }
            async Task Broadcast(GameEngineResult result)
            {
                Assert.True(result.Succeeded, result.Error?.Message);
                foreach (var reader in readers)
                {
                    var change = await ReadVersion(reader, result.State!.Version, timeout.Token);
                    Assert.Equal(id.ToString(), change.GetProperty("gameId").GetString());
                    Assert.False(change.TryGetProperty("answers", out _));
                }
                foreach (var client in clients)
                {
                    using var response = await client.GetAsync($"/api/v1/games/{id}/state", timeout.Token);
                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                    using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
                    Assert.Equal(result.State!.Version.ToString(System.Globalization.CultureInfo.InvariantCulture), body.RootElement.GetProperty("version").GetString());
                    Assert.False(body.RootElement.TryGetProperty("game", out _));
                }
            }
            await Broadcast(await engine.InitializeAsync(id));
            var started = await engine.StartAsync(id);
            await Broadcast(started);
            using (var state = JsonDocument.Parse(await clients[0].GetStringAsync($"/api/v1/games/{id}/state", timeout.Token)))
            {
                Assert.Equal(started.State!.CategorySelectionDeadline, state.RootElement.GetProperty("round").GetProperty("categorySelectionDeadline").GetDateTimeOffset());
            }
            async Task<PlayerActionContext> Context() => new(Guid.NewGuid(), (await engine.GetStateAsync(id)).State!.Version);
            await Broadcast(await engine.SelectCategoryAsync(id, players[0].Id, "european-capitals", await Context()));
            await Broadcast(await engine.PlaceBidAsync(id, players[0].Id, 1, await Context()));
            var challenged = await engine.CallBluffAsync(id, players[1].Id, await Context());
            await Broadcast(challenged);
            using (var state = JsonDocument.Parse(await clients[0].GetStringAsync($"/api/v1/games/{id}/state", timeout.Token)))
            {
                Assert.Equal(challenged.State!.Game.CurrentRound!.Challenge!.Deadline,
                    state.RootElement.GetProperty("round").GetProperty("challenge").GetProperty("deadline").GetDateTimeOffset());
            }
            await Broadcast(await engine.SubmitWordAsync(id, players[0].Id, "Paris", await Context()));
        }
        finally
        {
            foreach (var reader in readers)
            {
                reader.Dispose();
            }

            foreach (var response in responses)
            {
                response.Dispose();
            }

            foreach (var client in clients)
            {
                client.Dispose();
            }
        }
        var store = factory.Services.GetRequiredService<IGameSessionStore>();
        while ((await store.GetAsync(id, timeout.Token))!.Players.Any(player => player.ConnectionCount != 0))
        {
            await Task.Delay(10, timeout.Token);
        }
    }

    [Theory]
    [InlineData("state")]
    [InlineData("events")]
    public async Task PrivateReadsRejectMissingForgedExpiredAndNonmemberIdentities(string resource)
    {
        using var factory = new WordGameApiFactory();
        var engine = factory.Services.GetRequiredService<GameEngine>();
        var player = new Player(Guid.NewGuid(), "Alice");
        var game = await engine.CreateAsync([player, new Player(Guid.NewGuid(), "Bob")]);
        var path = $"/api/v1/games/{game.State!.Id}/{resource}";
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(path + $"?playerId={player.Id}")).StatusCode);
        anonymous.DefaultRequestHeaders.Add("Cookie", $"wordgame.player={player.Id}");
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(path)).StatusCode);
        using var expired = AuthenticatedPlayerClient.Create(factory, player.Id, DateTimeOffset.UtcNow.AddSeconds(-1));
        Assert.Equal(HttpStatusCode.Unauthorized, (await expired.GetAsync(path)).StatusCode);
        using var outsider = AuthenticatedPlayerClient.Create(factory, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync(path)).StatusCode);
        using var participant = AuthenticatedPlayerClient.Create(factory, player.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await participant.GetAsync($"/api/v1/games/{Guid.NewGuid()}/{resource}")).StatusCode);
    }

    [Fact]
    public async Task ReconnectionGetsLatestVersionAndRemovalClosesTheStream()
    {
        using var root = new WordGameApiFactory();
        using var factory = root.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Sessions:AbandonedRetention"] = "00:00:00" })));
        var engine = factory.Services.GetRequiredService<GameEngine>();
        var players = new[] { new Player(Guid.NewGuid(), "Alice"), new Player(Guid.NewGuid(), "Bob") };
        var created = await engine.CreateAsync(players);
        var id = created.State!.Id;
        using var client = AuthenticatedPlayerClient.Create(factory, players[0].Id);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using (var first = await client.GetAsync($"/api/v1/games/{id}/events", HttpCompletionOption.ResponseHeadersRead, timeout.Token))
        {
            using var reader = new StreamReader(await first.Content.ReadAsStreamAsync(timeout.Token));
            await ReadVersion(reader, 1, timeout.Token);
        }
        await engine.InitializeAsync(id);
        var started = await engine.StartAsync(id);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/games/{id}/events");
        request.Headers.Add("Last-Event-ID", "0");
        using var reconnected = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        using var stream = new StreamReader(await reconnected.Content.ReadAsStreamAsync(timeout.Token));
        await ReadVersion(stream, started.State!.Version, timeout.Token);
        var cancelled = await engine.CancelAsync(id);
        await ReadVersion(stream, cancelled.State!.Version, timeout.Token);
        await factory.Services.GetRequiredService<IGameSessionStore>().CleanupAsync(timeout.Token);
        var removed = await ReadVersion(stream, cancelled.State.Version + 1, timeout.Token);
        Assert.Equal("removed", removed.GetProperty("kind").GetString());
        Assert.Null(await stream.ReadLineAsync(timeout.Token));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/games/{id}/state", timeout.Token)).StatusCode);
    }

    [Fact]
    public async Task AnOpenStreamEndsAtCredentialExpiryAndReleasesPresence()
    {
        using var factory = new WordGameApiFactory();
        var engine = factory.Services.GetRequiredService<GameEngine>();
        var players = new[] { new Player(Guid.NewGuid(), "Alice"), new Player(Guid.NewGuid(), "Bob") };
        var created = await engine.CreateAsync(players);
        var id = created.State!.Id;
        using var client = AuthenticatedPlayerClient.Create(factory, players[0].Id, DateTimeOffset.UtcNow.AddSeconds(2));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var response = await client.GetAsync($"/api/v1/games/{id}/events", HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(timeout.Token));
        await ReadVersion(reader, 1, timeout.Token);
        Assert.Equal(string.Empty, await reader.ReadToEndAsync(timeout.Token));
        var state = (await engine.GetStateAsync(id)).State!;
        Assert.Equal(0, state.Players[0].ConnectionCount);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"/api/v1/games/{id}/state", timeout.Token)).StatusCode);
    }

    private static async Task<JsonElement> ReadVersion(StreamReader reader, long minimum, CancellationToken cancellationToken)
    {
        while (true)
        {
            string? line;
            string? data = null;
            string? id = null;
            string? eventType = null;
            while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
            {
                if (line.StartsWith("event: ", StringComparison.Ordinal))
                {
                    eventType = line[7..];
                }

                if (line.StartsWith("id: ", StringComparison.Ordinal))
                {
                    id = line[4..];
                }

                if (line.StartsWith("data: ", StringComparison.Ordinal))
                {
                    data = line[6..];
                }

                if (line.Length == 0 && data is not null)
                {
                    break;
                }
            }
            Assert.NotNull(data);
            if (eventType != "game")
            {
                continue;
            }

            using var document = JsonDocument.Parse(data);
            Assert.Equal(document.RootElement.GetProperty("version").GetString(), id);
            if (long.Parse(id!, System.Globalization.CultureInfo.InvariantCulture) >= minimum)
            {
                return document.RootElement.Clone();
            }
        }
    }
}
