using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WordGame.Api.Contracts.V1;
using WordGame.Api.Tests.Fixtures;
using WordGame.Application.Lobbies;
using WordGame.Application.Sessions;
using WordGame.Infrastructure.Sessions;

namespace WordGame.Api.Tests;

public sealed class LobbyCreationTests
{
    [Fact]
    public async Task GuestCreatesAWaitingLobbyWithSecureIdentityAndCanReplayTheRequest()
    {
        using var factory = new WordGameApiFactory();
        using var client = Client(factory);
        using var guestResponse = await client.PostAsJsonAsync("/api/v1/guests", new CreateGuestRequest());
        Assert.Equal(HttpStatusCode.OK, guestResponse.StatusCode);
        var cookie = Assert.Single(guestResponse.Headers.GetValues("Set-Cookie"));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        var guest = (await guestResponse.Content.ReadFromJsonAsync<GuestIdentityResponse>())!;
        Assert.NotEqual(Guid.Empty, guest.PlayerId);
        using var existing = await client.PostAsJsonAsync("/api/v1/guests", new CreateGuestRequest());
        Assert.Equal(guest.PlayerId, (await existing.Content.ReadFromJsonAsync<GuestIdentityResponse>())!.PlayerId);
        Assert.False(existing.Headers.Contains("Set-Cookie"));
        var request = new CreateLobbyRequest(Guid.NewGuid(), " Alice ");
        using var created = await client.PostAsJsonAsync("/api/v1/lobbies", request);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.True(created.Headers.CacheControl!.NoStore);
        var lobby = await ReadLobby(created, factory);
        Assert.NotEqual(Guid.Empty, lobby.Id);
        Assert.Matches("^[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{6}$", lobby.LobbyCode);
        Assert.Equal(guest.PlayerId, lobby.HostPlayerId);
        Assert.Equal(guest.PlayerId, Assert.Single(lobby.Players).Id);
        Assert.Equal("Alice", lobby.Players[0].DisplayName);
        Assert.Equal(ApiLobbyStatus.Waiting, lobby.Status);
        Assert.Equal(2, lobby.MinimumPlayers);
        Assert.Equal(4, lobby.MaximumPlayers);
        Assert.Equal(TimeSpan.FromMinutes(30), lobby.ExpiresAt - lobby.CreatedAt);
        var store = factory.Services.GetRequiredService<ILobbySessionStore>();
        Assert.Equal(lobby.HostPlayerId, (await store.GetAsync(lobby.Id))!.HostPlayerId);
        Assert.Empty(await factory.Services.GetRequiredService<IGameSessionStore>().GetActiveAsync());
        using var replay = await client.PostAsJsonAsync("/api/v1/lobbies", request with { DisplayName = "Alice" });
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(lobby.Id, (await ReadLobby(replay, factory)).Id);
        using var conflict = await client.PostAsJsonAsync("/api/v1/lobbies", request with { DisplayName = "Changed" });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal("application/problem+json", conflict.Content.Headers.ContentType!.MediaType);
        Assert.Equal("Alice", (await store.GetAsync(lobby.Id))!.Players[0].DisplayName);
    }

    [Fact]
    public async Task ConcurrentRequestsAreDeduplicatedAndGuestsOwnIndependentLobbies()
    {
        using var factory = new WordGameApiFactory();
        using var client = Client(factory);
        await EnsureGuest(client);
        var request = new CreateLobbyRequest(Guid.NewGuid(), "Alice");
        var responses = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => client.PostAsJsonAsync("/api/v1/lobbies", request)));
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
            Assert.All(responses, response => Assert.Contains(response.StatusCode, new[] { HttpStatusCode.Created, HttpStatusCode.OK }));
            var lobbies = await Task.WhenAll(responses.Select(response => ReadLobby(response, factory)));
            Assert.Single(lobbies.Select(lobby => lobby.Id).Distinct());
            using var secondClient = Client(factory);
            await EnsureGuest(secondClient);
            using var otherResponse = await secondClient.PostAsJsonAsync("/api/v1/lobbies", request);
            Assert.Equal(HttpStatusCode.Created, otherResponse.StatusCode);
            var other = await ReadLobby(otherResponse, factory);
            Assert.NotEqual(lobbies[0].Id, other.Id);
            Assert.NotEqual(lobbies[0].LobbyCode, other.LobbyCode);
            Assert.NotEqual(lobbies[0].HostPlayerId, other.HostPlayerId);
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Development")]
    public async Task InvalidBodiesReturnClearClientErrorsAndDoNotReserveCreationRequests(string environment)
    {
        using var factory = new WordGameApiFactory(environment);
        using var client = Client(factory);
        await EnsureGuest(client);
        var requestId = Guid.NewGuid();
        var valid = new Dictionary<string, object?> { ["requestId"] = requestId, ["displayName"] = "Alice" };
        var invalid = new List<string> { "null", "{", "{}" };
        foreach (var name in new[] { "missingRequest", "missingName", "emptyRequest", "badGuid", "blank", "long", "control", "nullName", "spoofHost" })
        {
            var body = new Dictionary<string, object?>(valid);
            switch (name)
            {
                case "missingRequest": body.Remove("requestId"); break;
                case "missingName": body.Remove("displayName"); break;
                case "emptyRequest": body["requestId"] = Guid.Empty; break;
                case "badGuid": body["requestId"] = "not-a-uuid"; break;
                case "blank": body["displayName"] = "   "; break;
                case "long": body["displayName"] = new string('A', 33); break;
                case "control": body["displayName"] = "Ali\nce"; break;
                case "nullName": body["displayName"] = null; break;
                case "spoofHost": body["hostPlayerId"] = Guid.NewGuid(); break;
            }
            invalid.Add(JsonSerializer.Serialize(body));
        }
        foreach (var body in invalid)
        {
            using var response = await client.PostAsync("/api/v1/lobbies", new StringContent(body, Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
            using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(400, problem.RootElement.GetProperty("status").GetInt32());
            Assert.False(string.IsNullOrWhiteSpace(problem.RootElement.GetProperty("title").GetString()));
        }
        using var wrongType = await client.PostAsync("/api/v1/lobbies", new StringContent(JsonSerializer.Serialize(valid)));
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, wrongType.StatusCode);
        using var created = await client.PostAsJsonAsync("/api/v1/lobbies", new CreateLobbyRequest(requestId, "Alice"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
    }

    [Fact]
    public async Task CreationRequiresVerifiedIdentityAndRejectsForeignBrowserOrigins()
    {
        using var factory = new WordGameApiFactory();
        using var client = Client(factory);
        var request = new CreateLobbyRequest(Guid.NewGuid(), "Alice");
        using var anonymous = await client.PostAsJsonAsync("/api/v1/lobbies", request);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        using var forged = Client(factory);
        forged.DefaultRequestHeaders.Add("Cookie", $"wordgame.player={Guid.NewGuid()}");
        using var rejected = await forged.PostAsJsonAsync("/api/v1/lobbies", request);
        Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
        client.DefaultRequestHeaders.Add("Origin", "https://foreign.example");
        using var guestDenied = await client.PostAsJsonAsync("/api/v1/guests", new CreateGuestRequest());
        Assert.Equal(HttpStatusCode.Forbidden, guestDenied.StatusCode);
        Assert.False(guestDenied.Headers.Contains("Set-Cookie"));
        client.DefaultRequestHeaders.Remove("Origin");
        await EnsureGuest(client);
        client.DefaultRequestHeaders.Add("Origin", "https://foreign.example");
        using var lobbyDenied = await client.PostAsJsonAsync("/api/v1/lobbies", request);
        Assert.Equal(HttpStatusCode.Forbidden, lobbyDenied.StatusCode);
        client.DefaultRequestHeaders.Remove("Origin");
        client.DefaultRequestHeaders.Add("Origin", "https://localhost");
        using var sameOrigin = await client.PostAsJsonAsync("/api/v1/lobbies", request);
        Assert.Equal(HttpStatusCode.Created, sameOrigin.StatusCode);
    }

    [Fact]
    public async Task SharedCleanupWorkerRemovesExpiredLobbies()
    {
        var observed = new ObservedLobbyStore(new InMemoryLobbySessionStore(TimeProvider.System, new LobbyRules(TimeSpan.FromMilliseconds(50))));
        using var root = new WordGameApiFactory();
        using var factory = root.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services => services.AddSingleton<ILobbySessionStore>(observed));
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Sessions:CleanupInterval"] = "00:00:00.020" }));
        });
        using var client = Client(factory);
        await EnsureGuest(client);
        using var response = await client.PostAsJsonAsync("/api/v1/lobbies", new CreateLobbyRequest(Guid.NewGuid(), "Alice"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var lobby = await ReadLobby(response, factory);
        Assert.Equal(1, await observed.Removed.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Null(await observed.GetAsync(lobby.Id));
    }

    private static HttpClient Client(WebApplicationFactory<Program> factory) => factory.CreateClient(
        new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    private static async Task EnsureGuest(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/guests", new CreateGuestRequest());
        response.EnsureSuccessStatusCode();
    }

    private static async Task<LobbyResponse> ReadLobby(HttpResponseMessage response, WebApplicationFactory<Program> factory) =>
        (await response.Content.ReadFromJsonAsync<LobbyResponse>(factory.Services.GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions))!;

    private sealed class ObservedLobbyStore(ILobbySessionStore inner) : ILobbySessionStore
    {
        public Task<WordGame.Domain.Lobby?> UpdateAsync(Guid id, Func<WordGame.Domain.Lobby, WordGame.Domain.Lobby> update, CancellationToken cancellationToken = default) => inner.UpdateAsync(id, update, cancellationToken);
        public Task<WordGame.Domain.Lobby?> ConfigureAsync(Guid id, Guid actor, long version, WordGame.Domain.LobbySettings settings, CancellationToken cancellationToken = default) => inner.ConfigureAsync(id, actor, version, settings, cancellationToken);
        public Task<WordGame.Domain.Lobby?> StartAsync(Guid id, Guid actor, long version, Func<WordGame.Domain.Lobby, Task<Guid>> createGame, CancellationToken cancellationToken = default) => inner.StartAsync(id, actor, version, createGame, cancellationToken);
        public IAsyncEnumerable<WordGame.Domain.Lobby?> WatchAsync(Guid id, CancellationToken cancellationToken = default) => inner.WatchAsync(id, cancellationToken);
        internal TaskCompletionSource<int> Removed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<LobbyCreation> CreateAsync(WordGame.Domain.Player host, Guid requestId, CancellationToken cancellationToken = default) =>
            inner.CreateAsync(host, requestId, cancellationToken);
        public Task<WordGame.Domain.Lobby?> GetAsync(Guid lobbyId, CancellationToken cancellationToken = default) => inner.GetAsync(lobbyId, cancellationToken);
        public Task<WordGame.Domain.Lobby?> JoinAsync(WordGame.Domain.LobbyCode code, WordGame.Domain.Player player, Guid? expectedLobbyId = null,
            CancellationToken cancellationToken = default) => inner.JoinAsync(code, player, expectedLobbyId, cancellationToken);
        public async Task<int> CleanupAsync(CancellationToken cancellationToken = default)
        {
            var count = await inner.CleanupAsync(cancellationToken);
            if (count > 0)
            {
                Removed.TrySetResult(count);
            }

            return count;
        }
    }
}
