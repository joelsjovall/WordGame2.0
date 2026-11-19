using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using WordGame.Api.Contracts.V1;
using WordGame.Api.Tests.Fixtures;

namespace WordGame.Api.Tests;

public sealed class LobbySynchronizationTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };

    [Fact]
    public async Task BothStreamsReceiveJoinsSettingsReadinessAndTheSameStartedGame()
    {
        using var factory = new WordGameApiFactory();
        using var host = await Guest(factory);
        using var bob = await Guest(factory);
        using var carol = await Guest(factory);
        var room = await Create(host);
        using var hostEvents = await Events(host, room.Id);
        await Join(bob, room, "Bob");
        await hostEvents.Until(state => state.Players.Count == 2);
        using var bobEvents = await Events(bob, room.Id);
        await Join(carol, room, "Carol");
        await Both(hostEvents, bobEvents, state => state.Players.Count == 3);
        using var leave = await carol.PostAsJsonAsync(Path(room.Id, "leave"), new LeaveLobbyRequest());
        leave.EnsureSuccessStatusCode();
        await Both(hostEvents, bobEvents, state => state.Players.Count == 2);

        var current = await State(host, room.Id);
        using var settings = await host.PostAsJsonAsync(Path(room.Id, "settings"), new UpdateLobbySettingsRequest(current.Version,
            new LobbySettingsRequest(3, 7, 75, [ApiCategoryDifficulty.Hard])), Json);
        settings.EnsureSuccessStatusCode();
        await Both(hostEvents, bobEvents, state => state.Settings.ChallengeSeconds == 75 && state.Settings.RoundCount == 7
            && state.MaximumPlayers == 3 && state.Settings.AllowedDifficulties.SequenceEqual([ApiCategoryDifficulty.Hard]));

        await Ready(host, room.Id);
        await Both(hostEvents, bobEvents, state => state.Players.Single(player => player.Id == room.HostPlayerId).IsReady);
        await Ready(bob, room.Id);
        await Both(hostEvents, bobEvents, state => state.CanStart && state.Players.All(player => player.IsReady && player.IsConnected));
        current = await State(host, room.Id);
        using var start = await host.PostAsJsonAsync(Path(room.Id, "start"), new StartLobbyRequest(current.Version));
        var started = await Read(start);
        Assert.NotNull(started.GameId);
        await Both(hostEvents, bobEvents, state => state.Status == ApiLobbyStatus.Started && state.GameId == started.GameId);
    }

    [Fact]
    public async Task RemovalHostMigrationAndReconnectionDeliverCurrentAuthoritativeMembership()
    {
        using var factory = new WordGameApiFactory();
        using var host = await Guest(factory);
        using var bob = await Guest(factory);
        using var carol = await Guest(factory);
        var room = await Create(host);
        await Join(bob, room, "Bob");
        await Join(carol, room, "Carol");
        using var hostEvents = await Events(host, room.Id);
        using var bobEvents = await Events(bob, room.Id);
        using var carolEvents = await Events(carol, room.Id);
        var current = await State(host, room.Id);
        var carolId = current.Players.Single(player => player.DisplayName == "Carol").Id;
        var bobId = current.Players.Single(player => player.DisplayName == "Bob").Id;
        using var remove = await host.PostAsJsonAsync(Path(room.Id, "remove"), new RemoveLobbyPlayerRequest(carolId));
        remove.EnsureSuccessStatusCode();
        await Both(hostEvents, bobEvents, state => state.Players.All(player => player.Id != carolId));
        await carolEvents.Removed();

        using var leave = await host.PostAsJsonAsync(Path(room.Id, "leave"), new LeaveLobbyRequest());
        leave.EnsureSuccessStatusCode();
        await bobEvents.Until(state => state.HostPlayerId == bobId && state.Players.Count == 1);
        await hostEvents.Removed();
        bobEvents.Dispose();

        // Change authoritative state while this client has no subscription, then reconnect without rejoining.
        current = await State(bob, room.Id);
        using var settings = await bob.PostAsJsonAsync(Path(room.Id, "settings"), new UpdateLobbySettingsRequest(current.Version,
            new LobbySettingsRequest(4, 9, 90, [ApiCategoryDifficulty.Easy])), Json);
        var updated = await Read(settings);
        using var reconnected = await Events(bob, room.Id);
        var recovered = await reconnected.Until(state => state.Settings.RoundCount == 9 && state.Settings.ChallengeSeconds == 90);
        Assert.True(long.Parse(recovered.Version) >= long.Parse(updated.Version));
        Assert.Equal(bobId, recovered.HostPlayerId);
        Assert.Equal(bobId, Assert.Single(recovered.Players).Id);
    }

    private static async Task Both(EventReader first, EventReader second, Func<LobbyResponse, bool> predicate)
    {
        await Task.WhenAll(first.Until(predicate), second.Until(predicate));
    }

    private static string Path(Guid id, string? action = null) => $"/api/v1/lobbies/{id}" + (action is null ? "" : $"/{action}");
    private static async Task<HttpClient> Guest(WordGameApiFactory factory)
    {
        var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var response = await client.PostAsJsonAsync("/api/v1/guests", new CreateGuestRequest());
        response.EnsureSuccessStatusCode();
        return client;
    }
    private static async Task<LobbyResponse> Create(HttpClient host)
    {
        using var response = await host.PostAsJsonAsync("/api/v1/lobbies", new CreateLobbyRequest(Guid.NewGuid(), "Host"));
        return await Read(response);
    }
    private static async Task Join(HttpClient client, LobbyResponse room, string name)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/lobbies/join", new JoinLobbyRequest(room.LobbyCode, name));
        response.EnsureSuccessStatusCode();
    }
    private static async Task<LobbyResponse> State(HttpClient client, Guid id)
    {
        using var response = await client.GetAsync(Path(id));
        return await Read(response);
    }
    private static async Task<LobbyResponse> Read(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LobbyResponse>(Json))!;
    }
    private static async Task Ready(HttpClient client, Guid id)
    {
        var state = await State(client, id);
        using var response = await client.PostAsJsonAsync(Path(id, "ready"), new SetLobbyReadyRequest(state.Version, true));
        response.EnsureSuccessStatusCode();
    }
    private static async Task<EventReader> Events(HttpClient client, Guid id)
    {
        var response = await client.GetAsync(Path(id, "events"), HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType!.MediaType);
        return new EventReader(response, new StreamReader(await response.Content.ReadAsStreamAsync()));
    }

    private sealed class EventReader(HttpResponseMessage response, StreamReader reader) : IDisposable
    {
        public async Task<(string Type, LobbyResponse State)> Next()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var type = "";
            while (await reader.ReadLineAsync(timeout.Token) is { } line)
            {
                if (line.StartsWith("event:", StringComparison.Ordinal))
                {
                    type = line[6..].Trim();
                }
                if (line.StartsWith("data:", StringComparison.Ordinal))
                {
                    return (type, JsonSerializer.Deserialize<LobbyResponse>(line[5..], Json)!);
                }
            }
            throw new InvalidOperationException("Lobby stream ended before the expected notification.");
        }
        public async Task<LobbyResponse> Until(Func<LobbyResponse, bool> predicate)
        {
            // Bound the entire assertion, including repeated snapshots, rather than waiting indefinitely.
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (true)
            {
                var item = await Next().WaitAsync(timeout.Token);
                Assert.Equal("lobby", item.Type);
                if (predicate(item.State))
                {
                    return item.State;
                }
            }
        }
        public async Task Removed()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (true)
            {
                var item = await Next().WaitAsync(timeout.Token);
                if (item.Type == "removed")
                {
                    return;
                }
                Assert.Equal("lobby", item.Type);
            }
        }
        public void Dispose() => response.Dispose();
    }
}
