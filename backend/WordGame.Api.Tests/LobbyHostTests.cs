using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using WordGame.Api.Contracts.V1;
using WordGame.Api.Tests.Fixtures;
using WordGame.Application.Sessions;
using WordGame.Domain;

namespace WordGame.Api.Tests;

public sealed class LobbyHostTests
{
    [Fact]
    public async Task HostSettingsFreezeIntoSharedEngineAndStartRetriesReturnOneGame()
    {
        using var factory = new WordGameApiFactory();
        using var host = await Guest(factory);
        using var bob = await Guest(factory);
        var room = await Create(host);
        var id = room.GetProperty("id").GetGuid();
        using var tooFew = await host.PostAsJsonAsync($"/api/v1/lobbies/{id}/start", new StartLobbyRequest("0"));
        Assert.Equal(HttpStatusCode.Conflict, tooFew.StatusCode);
        using var joined = await bob.PostAsJsonAsync("/api/v1/lobbies/join", new JoinLobbyRequest(room.GetProperty("lobbyCode").GetString()!, "Bob"));
        Assert.Equal(HttpStatusCode.OK, joined.StatusCode);
        var initial = await Read(joined);
        var version = initial.GetProperty("version").GetString();
        using var denied = await bob.PostAsJsonAsync($"/api/v1/lobbies/{id}/settings", Settings(version!));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var deniedStart = await bob.PostAsJsonAsync($"/api/v1/lobbies/{id}/start", new StartLobbyRequest(version!));
        Assert.Equal(HttpStatusCode.Forbidden, deniedStart.StatusCode);
        using var changed = await host.PostAsJsonAsync($"/api/v1/lobbies/{id}/settings", Settings(version!));
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        var configured = await Read(changed);
        Assert.Equal(2, configured.GetProperty("maximumPlayers").GetInt32());
        using var stale = await host.PostAsJsonAsync($"/api/v1/lobbies/{id}/settings", Settings(version!));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        version = configured.GetProperty("version").GetString();
        var lobbies = factory.Services.GetRequiredService<ILobbySessionStore>();
        var lobby = (await lobbies.GetAsync(id))!;
        foreach (var player in lobby.Players)
        {
            lobby = (await lobbies.UpdateAsync(id, current => current.Connect(player.Id, Guid.NewGuid()).SetReady(player.Id, true)))!;
        }

        version = lobby.Version.ToString(System.Globalization.CultureInfo.InvariantCulture);
        using var started = await host.PostAsJsonAsync($"/api/v1/lobbies/{id}/start", new StartLobbyRequest(version!));
        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        var committed = await Read(started);
        Assert.Equal("started", committed.GetProperty("status").GetString());
        var gameId = committed.GetProperty("gameId").GetGuid();
        using var repeat = await host.PostAsJsonAsync($"/api/v1/lobbies/{id}/start", new StartLobbyRequest(version!));
        Assert.Equal(gameId, (await Read(repeat)).GetProperty("gameId").GetGuid());
        using var locked = await host.PostAsJsonAsync($"/api/v1/lobbies/{id}/settings", Settings(committed.GetProperty("version").GetString()!));
        Assert.Equal(HttpStatusCode.Conflict, locked.StatusCode);
        var game = await factory.Services.GetRequiredService<IGameSessionStore>().GetAsync(gameId);
        Assert.Equal(SessionStatus.Active, game!.Status);
        Assert.Equal(7, game.Game.Settings.RoundCount);
        Assert.Equal(TimeSpan.FromSeconds(75), game.Game.Settings.ChallengeDuration);
        Assert.Equal(CategoryDifficulty.Hard, Assert.Single(game.Game.Settings.AllowedDifficulties!));
        Assert.Equal(2, game.Game.Players.Count);
        using var publicGame = await bob.GetAsync($"/api/v1/games/{gameId}/state");
        var match = (await Read(publicGame)).GetProperty("match");
        Assert.Equal(75, match.GetProperty("challengeSeconds").GetInt32());
        Assert.Equal("hard", Assert.Single(match.GetProperty("allowedDifficulties").EnumerateArray()).GetString());
        Assert.Single(await factory.Services.GetRequiredService<IGameSessionStore>().GetActiveAsync());
    }

    [Theory]
    [InlineData(1, 4, 30, "easy")]
    [InlineData(5, 4, 30, "easy")]
    [InlineData(4, 0, 30, "easy")]
    [InlineData(4, 101, 30, "easy")]
    [InlineData(4, 4, 4, "easy")]
    [InlineData(4, 4, 301, "easy")]
    [InlineData(4, 4, 30, "expert")]
    public async Task InvalidSettingsNeverChangeTheLobby(int maximumPlayers, int roundCount, int challengeSeconds, string difficulty)
    {
        using var factory = new WordGameApiFactory();
        using var host = await Guest(factory);
        var room = await Create(host);
        var id = room.GetProperty("id").GetGuid();
        using var response = await host.PostAsJsonAsync($"/api/v1/lobbies/{id}/settings", new
        {
            expectedVersion = "0",
            settings = new { maximumPlayers, roundCount, challengeSeconds, allowedDifficulties = new[] { difficulty } }
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var current = await factory.Services.GetRequiredService<ILobbySessionStore>().GetAsync(id);
        Assert.Equal(0, current!.Version);
        Assert.Equal(4, current.Settings.MaximumPlayers);
    }

    [Fact]
    public async Task LobbyReadsAndLiveSettingsRequireMembershipAndUpdatesUseOriginChecks()
    {
        using var factory = new WordGameApiFactory();
        using var host = await Guest(factory);
        using var outsider = await Guest(factory);
        using var anonymous = factory.CreateClient();
        var room = await Create(host);
        var id = room.GetProperty("id").GetGuid();
        using var unauthorized = await anonymous.GetAsync($"/api/v1/lobbies/{id}");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        using var hidden = await outsider.GetAsync($"/api/v1/lobbies/{id}");
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        using var hiddenEvents = await outsider.GetAsync($"/api/v1/lobbies/{id}/events");
        Assert.Equal(HttpStatusCode.NotFound, hiddenEvents.StatusCode);
        using var events = await host.GetAsync($"/api/v1/lobbies/{id}/events", HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal("text/event-stream", events.Content.Headers.ContentType!.MediaType);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var reader = new StreamReader(await events.Content.ReadAsStreamAsync(cancellation.Token));
        Assert.Contains("\"version\":\"1\"", await Data(reader, cancellation.Token));
        using var update = await host.PostAsJsonAsync($"/api/v1/lobbies/{id}/settings", Settings("1"));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var payload = await Data(reader, cancellation.Token);
        Assert.Contains("\"challengeSeconds\":75", payload);
        Assert.Contains("\"version\":\"2\"", payload);
        host.DefaultRequestHeaders.Add("Origin", "https://foreign.example");
        using var foreign = await host.PostAsJsonAsync($"/api/v1/lobbies/{id}/settings", Settings("1"));
        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
        events.Dispose();
    }

    private static object Settings(string version) => new
    {
        expectedVersion = version,
        settings = new { maximumPlayers = 2, roundCount = 7, challengeSeconds = 75, allowedDifficulties = new[] { "hard" } }
    };
    private static async Task<HttpClient> Guest(WordGameApiFactory factory)
    {
        var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var response = await client.PostAsJsonAsync("/api/v1/guests", new CreateGuestRequest());
        response.EnsureSuccessStatusCode(); return client;
    }
    private static async Task<JsonElement> Create(HttpClient host)
    {
        using var response = await host.PostAsJsonAsync("/api/v1/lobbies", new CreateLobbyRequest(Guid.NewGuid(), "Host"));
        response.EnsureSuccessStatusCode(); return await Read(response);
    }
    private static async Task<JsonElement> Read(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()); return json.RootElement.Clone();
    }
    private static async Task<string> Data(StreamReader reader, CancellationToken cancellationToken)
    {
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                return line;
            }
        }

        throw new InvalidOperationException("The event stream ended before a snapshot.");
    }
}
