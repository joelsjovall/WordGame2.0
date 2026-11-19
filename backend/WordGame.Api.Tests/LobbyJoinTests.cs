using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using WordGame.Api.Contracts.V1;
using WordGame.Api.Tests.Fixtures;
using WordGame.Application.Sessions;

namespace WordGame.Api.Tests;

public sealed class LobbyJoinTests
{
    [Fact]
    public async Task GuestsJoinByCodeOrInvitationAndCannotTakeDuplicateSlotsOrNames()
    {
        using var factory = new WordGameApiFactory();
        using var host = await Guest(factory);
        using var creation = await host.PostAsJsonAsync("/api/v1/lobbies", new CreateLobbyRequest(Guid.NewGuid(), "Host"));
        using var roomJson = JsonDocument.Parse(await creation.Content.ReadAsStringAsync());
        var id = roomJson.RootElement.GetProperty("id").GetGuid();
        var code = roomJson.RootElement.GetProperty("lobbyCode").GetString()!;
        using var bob = await Guest(factory);
        using var first = await bob.PostAsJsonAsync("/api/v1/lobbies/join", new JoinLobbyRequest($" {code.ToLowerInvariant()} ", "Bob", id));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using var replay = await bob.PostAsJsonAsync("/api/v1/lobbies/join", new JoinLobbyRequest(code, " bob "));
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        using var renamed = await bob.PostAsJsonAsync("/api/v1/lobbies/join", new JoinLobbyRequest(code, "Changed"));
        Assert.Equal(HttpStatusCode.Conflict, renamed.StatusCode);
        using var carol = await Guest(factory);
        using var duplicateName = await carol.PostAsJsonAsync("/api/v1/lobbies/join", new JoinLobbyRequest(code, "BOB"));
        Assert.Equal(HttpStatusCode.Conflict, duplicateName.StatusCode);
        using var wrongInvitation = await carol.PostAsJsonAsync("/api/v1/lobbies/join", new JoinLobbyRequest(code, "Carol", Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.NotFound, wrongInvitation.StatusCode);
        using var correct = await carol.PostAsJsonAsync("/api/v1/lobbies/join", new JoinLobbyRequest(code, "Carol"));
        Assert.Equal(HttpStatusCode.OK, correct.StatusCode);
        using var dan = await Guest(factory);
        using var fourth = await dan.PostAsJsonAsync("/api/v1/lobbies/join", new JoinLobbyRequest(code, "Dan"));
        Assert.Equal(HttpStatusCode.OK, fourth.StatusCode);
        using var eve = await Guest(factory);
        using var full = await eve.PostAsJsonAsync("/api/v1/lobbies/join", new JoinLobbyRequest(code, "Eve"));
        Assert.Equal(HttpStatusCode.Conflict, full.StatusCode);
        using var error = JsonDocument.Parse(await full.Content.ReadAsStringAsync());
        Assert.Equal("This lobby is full.", error.RootElement.GetProperty("title").GetString());
        var stored = await factory.Services.GetRequiredService<ILobbySessionStore>().GetAsync(id);
        Assert.Equal(new[] { "Host", "Bob", "Carol", "Dan" }, stored!.Players.Select(player => player.DisplayName));
    }

    [Fact]
    public async Task ExpiredMalformedAndUnauthorizedJoinsReturnClearErrors()
    {
        var clock = new ControlledGameplayClock();
        using var root = new WordGameApiFactory();
        using var factory = root.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<TimeProvider>(clock)));
        using var anonymous = factory.CreateClient();
        using var unauthorized = await anonymous.PostAsJsonAsync("/api/v1/lobbies/join", new JoinLobbyRequest("ABC234", "Bob"));
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        using var guest = await Guest(factory);
        using var invalid = await guest.PostAsJsonAsync("/api/v1/lobbies/join", new JoinLobbyRequest("bad", "Bob"));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var missing = await guest.PostAsJsonAsync("/api/v1/lobbies/join", new JoinLobbyRequest("ABC234", "Bob"));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using var spoofed = await guest.PostAsJsonAsync("/api/v1/lobbies/join", new { lobbyCode = "ABC234", displayName = "Bob", playerId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.BadRequest, spoofed.StatusCode);
        using var creation = await guest.PostAsJsonAsync("/api/v1/lobbies", new CreateLobbyRequest(Guid.NewGuid(), "Host"));
        using var room = JsonDocument.Parse(await creation.Content.ReadAsStringAsync());
        var actualCode = room.RootElement.GetProperty("lobbyCode").GetString()!;
        clock.Advance(TimeSpan.FromMinutes(30));
        using var expired = await guest.PostAsJsonAsync("/api/v1/lobbies/join", new JoinLobbyRequest(actualCode, "Host"));
        Assert.Equal(HttpStatusCode.NotFound, expired.StatusCode);
        guest.DefaultRequestHeaders.Add("Origin", "https://foreign.example");
        using var foreign = await guest.PostAsJsonAsync("/api/v1/lobbies/join", new JoinLobbyRequest("ABC234", "Bob"));
        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
    }

    private static async Task<HttpClient> Guest(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        using var response = await client.PostAsJsonAsync("/api/v1/guests", new CreateGuestRequest());
        response.EnsureSuccessStatusCode();
        return client;
    }
}
