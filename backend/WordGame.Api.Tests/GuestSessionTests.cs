using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WordGame.Api.Contracts.V1;
using WordGame.Api.Tests.Fixtures;
using WordGame.Application.Games;
using WordGame.Application.Sessions;
using WordGame.Domain;

namespace WordGame.Api.Tests;

public sealed class GuestSessionTests
{
    [Fact]
    public async Task ProductionProxyAcceptsConfiguredHttpsOriginOnly()
    {
        using var root = new WordGameApiFactory();
        using var factory = root.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["Public:Origin"] = "https://wordgame.example" })));
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("http://internal-host") });
        client.DefaultRequestHeaders.Add("Origin", "https://wordgame.example");
        using var accepted = await client.PostAsJsonAsync("/api/v1/guests", new CreateGuestRequest());
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        client.DefaultRequestHeaders.Remove("Origin");
        client.DefaultRequestHeaders.Add("Origin", "https://foreign.example");
        using var rejected = await client.PostAsJsonAsync("/api/v1/guests", new CreateGuestRequest());
        Assert.Equal(HttpStatusCode.Forbidden, rejected.StatusCode);
    }

    [Fact]
    public async Task ServerExpiryRejectsAnOtherwiseProtectedCookieAndBootstrapDoesNotRenewItsPlayerId()
    {
        var clock = new ControlledGameplayClock();
        using var root = new WordGameApiFactory();
        using var factory = root.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<TimeProvider>(clock)));
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var original = await Identity(client);
        clock.Advance(TimeSpan.FromDays(1));
        using var denied = await client.GetAsync($"/api/v1/lobbies/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        Assert.NotEqual(original, await Identity(client));
    }

    [Fact]
    public async Task ResetRevokesCopiedCredentialsEndsLobbyStreamAndCreatesANewIdentityOnlyOnNextBootstrap()
    {
        using var factory = new WordGameApiFactory();
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var bootstrap = await client.PostAsJsonAsync("/api/v1/guests", new CreateGuestRequest());
        var identity = (await bootstrap.Content.ReadFromJsonAsync<GuestIdentityResponse>())!;
        var cookie = bootstrap.Headers.GetValues("Set-Cookie").Single().Split(';')[0];
        using var copied = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), HandleCookies = false });
        copied.DefaultRequestHeaders.Add("Cookie", cookie);
        using var create = await client.PostAsJsonAsync("/api/v1/lobbies", new CreateLobbyRequest(Guid.NewGuid(), "Host"));
        using var room = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var lobbyId = room.RootElement.GetProperty("id").GetGuid();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var events = await client.GetAsync($"/api/v1/lobbies/{lobbyId}/events", HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        using var reader = new StreamReader(await events.Content.ReadAsStreamAsync(timeout.Token));
        await FirstEvent(reader, timeout.Token);
        using var reset = await client.PostAsJsonAsync("/api/v1/guests/reset", new CreateGuestRequest());
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.True(reset.Headers.CacheControl!.NoStore);
        Assert.Contains("expires=", reset.Headers.GetValues("Set-Cookie").Single(), StringComparison.OrdinalIgnoreCase);
        _ = await reader.ReadToEndAsync(timeout.Token);
        var lobby = await factory.Services.GetRequiredService<ILobbySessionStore>().GetAsync(lobbyId);
        Assert.False(lobby!.Roster.Members[identity.PlayerId].IsConnected);
        foreach (var browser in new[] { client, copied })
        {
            using var state = await browser.GetAsync($"/api/v1/lobbies/{lobbyId}");
            using var deniedReset = await browser.PostAsJsonAsync("/api/v1/guests/reset", new CreateGuestRequest());
            Assert.Equal(HttpStatusCode.Unauthorized, state.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, deniedReset.StatusCode);
        }
        using var renewed = await client.PostAsJsonAsync("/api/v1/guests", new CreateGuestRequest());
        Assert.NotEqual(identity.PlayerId, (await renewed.Content.ReadFromJsonAsync<GuestIdentityResponse>())!.PlayerId);
        using var noInheritance = await client.GetAsync($"/api/v1/lobbies/{lobbyId}");
        Assert.Equal(HttpStatusCode.NotFound, noInheritance.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SameGuestInfrastructureAuthorizesBothFormatsAndResetEndsGameSubscriptions(bool online)
    {
        using var factory = new WordGameApiFactory();
        using var alice = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var bob = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var aliceId = await Identity(alice);
        var bobId = await Identity(bob);
        Assert.NotEqual(aliceId, bobId);
        Assert.Equal(aliceId, await Identity(alice));
        var engine = factory.Services.GetRequiredService<GameEngine>();
        var game = await engine.CreateAsync([new Player(aliceId, "Alice"), new Player(bobId, "Bob")],
            online ? MatchSettings.Online() : MatchSettings.Party(1));
        var id = game.State!.Id;
        using var state = await alice.GetAsync($"/api/v1/games/{id}/state");
        Assert.Equal(HttpStatusCode.OK, state.StatusCode);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var events = await alice.GetAsync($"/api/v1/games/{id}/events", HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        using var reader = new StreamReader(await events.Content.ReadAsStreamAsync(timeout.Token));
        await FirstEvent(reader, timeout.Token);
        using var reset = await alice.PostAsJsonAsync("/api/v1/guests/reset", new CreateGuestRequest());
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        _ = await reader.ReadToEndAsync(timeout.Token);
        Assert.Equal(0, (await engine.GetStateAsync(id)).State!.Players.Single(player => player.PlayerId == aliceId).ConnectionCount);
        using var denied = await alice.GetAsync($"/api/v1/games/{id}/state");
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        using var retained = await bob.GetAsync($"/api/v1/games/{id}/state");
        Assert.Equal(HttpStatusCode.OK, retained.StatusCode);
    }

    [Fact]
    public async Task ResetRequiresSameOriginAndCannotRevokeAPlayerChosenInTheBody()
    {
        using var factory = new WordGameApiFactory();
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var player = await Identity(client);
        client.DefaultRequestHeaders.Add("Origin", "https://foreign.example");
        using var foreign = await client.PostAsJsonAsync("/api/v1/guests/reset", new CreateGuestRequest());
        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
        client.DefaultRequestHeaders.Remove("Origin");
        using var spoofed = await client.PostAsJsonAsync("/api/v1/guests/reset", new { playerId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.BadRequest, spoofed.StatusCode);
        Assert.Equal(player, await Identity(client));
    }

    private static async Task<Guid> Identity(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/guests", new CreateGuestRequest());
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<GuestIdentityResponse>())!.PlayerId;
    }
    private static async Task FirstEvent(StreamReader reader, CancellationToken cancellationToken)
    {
        var received = false;
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                received = true;
            }
            if (received && line.Length == 0)
            {
                return;
            }
        }
        Assert.Fail("Stream ended without an initial event.");
    }
}
