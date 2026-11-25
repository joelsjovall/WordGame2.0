using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using WordGame.Api.Contracts.V1;
using WordGame.Api.Tests.Fixtures;
using WordGame.Application.Sessions;
using WordGame.Domain;

namespace WordGame.Api.Tests;

public sealed class SpectatorTests
{
    [Fact]
    public async Task LobbyCodeGrantsReadOnlySpectatorAccessWithoutJoining()
    {
        using var factory = new WordGameApiFactory();
        using var client = factory.CreateClient();
        var store = factory.Services.GetRequiredService<ILobbySessionStore>();
        var lobby = (await store.CreateAsync(new Player(Guid.NewGuid(), "Host"), Guid.NewGuid())).Lobby;
        var path = $"/api/v1/lobbies/{lobby.Id}/spectate/{lobby.Code.Value}";

        var state = await client.GetFromJsonAsync<SpectatorStateResponse>(path);
        Assert.NotNull(state);
        Assert.Equal("waiting", state.LobbyStatus);
        Assert.Null(state.Game);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path + "X")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/v1/lobbies/{Guid.NewGuid()}/spectate/{lobby.Code.Value}")).StatusCode);
        Assert.False((await client.PostAsync(path, null)).IsSuccessStatusCode);
    }
}
