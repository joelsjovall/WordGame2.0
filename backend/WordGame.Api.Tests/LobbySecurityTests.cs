using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using WordGame.Api.Contracts.V1;
using WordGame.Api.Tests.Fixtures;

namespace WordGame.Api.Tests;

public sealed class LobbySecurityTests
{
    [Theory]
    [InlineData("/api/v1/lobbies", "CreatePermitsPerMinute")]
    [InlineData("/api/v1/lobbies/join", "JoinPermitsPerMinute")]
    [InlineData("/api/v1/guests", "GuestPermitsPerMinute")]
    [InlineData("/api/v1/matchmaking/searches", "MatchmakingPermitsPerMinute")]
    public async Task SensitiveEndpointsLimitAttemptsAcrossCookiesCodesAndSpoofedForwardingHeaders(string path, string setting)
    {
        using var root = new WordGameApiFactory();
        using var factory = root.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { [$"LobbyRateLimits:{setting}"] = "2" })));
        for (var attempt = 0; attempt < 3; attempt++)
        {
            // Every attempt has a different identity and untrusted forwarding value but the same connection IP.
            using var client = AuthenticatedPlayerClient.Create(factory, Guid.NewGuid());
            client.DefaultRequestHeaders.Add("X-Forwarded-For", $"192.0.2.{attempt + 1}");
            object body = path.EndsWith("/join", StringComparison.Ordinal)
                ? new JoinLobbyRequest(attempt == 0 ? "AAAAAA" : "BBBBBB", "Guest")
                : path.EndsWith("/guests", StringComparison.Ordinal) ? new CreateGuestRequest()
                : path.EndsWith("/searches", StringComparison.Ordinal) ? new JoinMatchmakingRequest("Guest")
                : new CreateLobbyRequest(Guid.NewGuid(), "Host");
            using var response = await client.PostAsJsonAsync(path, body);
            if (attempt < 2)
            {
                Assert.Equal(path.EndsWith("/join", StringComparison.Ordinal) ? HttpStatusCode.NotFound
                    : path.EndsWith("/guests", StringComparison.Ordinal) || path.EndsWith("/searches", StringComparison.Ordinal) ? HttpStatusCode.OK : HttpStatusCode.Created, response.StatusCode);
            }
            else
            {
                Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
                Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
                Assert.True(response.Headers.RetryAfter!.Delta > TimeSpan.Zero);
                using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                Assert.Equal(429, problem.RootElement.GetProperty("status").GetInt32());
            }
        }
    }

    [Fact]
    public async Task AnonymousForgedAndExpiredIdentitiesCannotAccessAnyProtectedLobbyOperation()
    {
        using var factory = new WordGameApiFactory();
        using var host = await Guest(factory);
        var room = await Create(host);
        using var anonymous = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var forged = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        forged.DefaultRequestHeaders.Add("Cookie", $"wordgame.player={room.HostPlayerId}");
        forged.DefaultRequestHeaders.Add("X-Player-Id", room.HostPlayerId.ToString());
        using var expired = AuthenticatedPlayerClient.Create(factory, room.HostPlayerId, DateTimeOffset.UtcNow.AddMinutes(-5));
        foreach (var client in new[] { anonymous, forged, expired })
        {
            foreach (var action in new[] { "settings", "start", "rematch", "remove", "ready", "leave" })
            {
                using var response = await client.PostAsJsonAsync(Path(room.Id, action), Body(action, room));
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            }
            using var state = await client.GetAsync(Path(room.Id));
            using var events = await client.GetAsync(Path(room.Id, "events"));
            using var create = await client.PostAsJsonAsync("/api/v1/lobbies", new CreateLobbyRequest(Guid.NewGuid(), "Spoof"));
            using var join = await client.PostAsJsonAsync("/api/v1/lobbies/join", new JoinLobbyRequest(room.LobbyCode, "Spoof"));
            Assert.All(new[] { state, events, create, join }, response => Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode));
        }
    }

    [Fact]
    public async Task KnowingTheInvitationOrSpoofingActorFieldsNeverGrantsHostPermissions()
    {
        using var factory = new WordGameApiFactory();
        using var host = await Guest(factory);
        using var guest = await Guest(factory);
        using var outsider = await Guest(factory);
        var room = await Create(host);
        using var joined = await guest.PostAsJsonAsync("/api/v1/lobbies/join", new JoinLobbyRequest(room.LobbyCode, "Guest", room.Id));
        joined.EnsureSuccessStatusCode();
        foreach (var client in new[] { guest, outsider })
        {
            client.DefaultRequestHeaders.Add("X-Player-Id", room.HostPlayerId.ToString());
            foreach (var action in new[] { "settings", "start", "rematch", "remove" })
            {
                using var response = await client.PostAsJsonAsync(Path(room.Id, action), Body(action, room));
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            }
        }
        using var hidden = await outsider.GetAsync(Path(room.Id));
        using var hiddenEvents = await outsider.GetAsync(Path(room.Id, "events"));
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, hiddenEvents.StatusCode);
        using var impersonated = await guest.PostAsJsonAsync(Path(room.Id, "ready"), new
        {
            expectedVersion = "1",
            isReady = true,
            playerId = room.HostPlayerId
        });
        Assert.Equal(HttpStatusCode.BadRequest, impersonated.StatusCode);
        using var current = await host.GetAsync(Path(room.Id));
        using var state = JsonDocument.Parse(await current.Content.ReadAsStringAsync());
        Assert.Equal(room.HostPlayerId, state.RootElement.GetProperty("hostPlayerId").GetGuid());
        Assert.All(state.RootElement.GetProperty("players").EnumerateArray(), player => Assert.False(player.GetProperty("isReady").GetBoolean()));
    }

    [Fact]
    public async Task UnknownAndMismatchedInvitationsHaveTheSameFailureAndDoNotRevealRoster()
    {
        using var factory = new WordGameApiFactory();
        using var host = await Guest(factory);
        using var outsider = await Guest(factory);
        var room = await Create(host);
        string? title = null;
        foreach (var request in new[] { new JoinLobbyRequest(room.LobbyCode, "Guest", Guid.NewGuid()), new JoinLobbyRequest("AAAAAA", "Guest", room.Id) })
        {
            using var response = await outsider.PostAsJsonAsync("/api/v1/lobbies/join", request);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var next = problem.RootElement.GetProperty("title").GetString();
            title ??= next;
            Assert.Equal(title, next);
            Assert.False(problem.RootElement.TryGetProperty("players", out _));
        }
    }

    [Fact]
    public async Task JoiningValidatesNamesAndPreservesTrimmedText()
    {
        using var factory = new WordGameApiFactory();
        using var host = await Guest(factory);
        using var guest = await Guest(factory);
        var room = await Create(host);
        foreach (var name in new[] { "   ", new string('a', 33), "bad\u0001name" })
        {
            using var invalid = await guest.PostAsJsonAsync("/api/v1/lobbies/join", new JoinLobbyRequest(room.LobbyCode, name));
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        }
        const string markup = "<img src=x onerror=alert(1)>";
        using var response = await guest.PostAsJsonAsync("/api/v1/lobbies/join", new JoinLobbyRequest(room.LobbyCode, $" {markup} "));
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadAsStringAsync();
        Assert.Equal("application/json", response.Content.Headers.ContentType!.MediaType);
        using var state = JsonDocument.Parse(payload);
        Assert.Equal(markup, state.RootElement.GetProperty("players")[1].GetProperty("displayName").GetString());
        Assert.Equal(room.HostPlayerId, state.RootElement.GetProperty("hostPlayerId").GetGuid());
    }

    [Fact]
    public async Task RemovedIdentityLosesEveryProtectedLobbyOperationAndCannotRejoin()
    {
        using var factory = new WordGameApiFactory();
        using var host = await Guest(factory);
        using var guest = await Guest(factory);
        var room = await Create(host);
        using var join = await guest.PostAsJsonAsync("/api/v1/lobbies/join", new JoinLobbyRequest(room.LobbyCode, "Guest"));
        join.EnsureSuccessStatusCode();
        using var joined = JsonDocument.Parse(await join.Content.ReadAsStringAsync());
        var guestId = joined.RootElement.GetProperty("players")[1].GetProperty("id").GetGuid();
        using var remove = await host.PostAsJsonAsync(Path(room.Id, "remove"), new RemoveLobbyPlayerRequest(guestId));
        remove.EnsureSuccessStatusCode();
        foreach (var action in new[] { "settings", "start", "rematch", "remove", "ready", "leave" })
        {
            using var response = await guest.PostAsJsonAsync(Path(room.Id, action), Body(action, room));
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        using var state = await guest.GetAsync(Path(room.Id));
        using var events = await guest.GetAsync(Path(room.Id, "events"));
        using var rejoin = await guest.PostAsJsonAsync("/api/v1/lobbies/join", new JoinLobbyRequest(room.LobbyCode, "Guest", room.Id));
        Assert.Equal(HttpStatusCode.NotFound, state.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, events.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, rejoin.StatusCode);
    }

    private static string Path(Guid id, string? action = null) => $"/api/v1/lobbies/{id}" + (action is null ? "" : $"/{action}");
    private static object Body(string action, LobbyResponse room) => action switch
    {
        "settings" => new { expectedVersion = "1", settings = new { maximumPlayers = 4, roundCount = 4, challengeSeconds = 30, allowedDifficulties = new[] { "easy" } } },
        "start" or "rematch" => new StartLobbyRequest("1"),
        "remove" => new RemoveLobbyPlayerRequest(room.HostPlayerId),
        "ready" => new SetLobbyReadyRequest("1", true),
        _ => new LeaveLobbyRequest()
    };
    private static async Task<HttpClient> Guest(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var response = await client.PostAsJsonAsync("/api/v1/guests", new CreateGuestRequest());
        response.EnsureSuccessStatusCode();
        return client;
    }
    private static async Task<LobbyResponse> Create(HttpClient host)
    {
        using var response = await host.PostAsJsonAsync("/api/v1/lobbies", new CreateLobbyRequest(Guid.NewGuid(), "Host"));
        response.EnsureSuccessStatusCode();
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        json.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return (await response.Content.ReadFromJsonAsync<LobbyResponse>(json))!;
    }
}
