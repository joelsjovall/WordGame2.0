using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using WordGame.Api.Contracts.V1;
using WordGame.Api.Tests.Fixtures;
using WordGame.Application.Categories;
using WordGame.Application.Sessions;
using WordGame.Domain;
using WordGame.Infrastructure.Categories;

namespace WordGame.Api.Tests;

public sealed class LobbyPlayerTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };

    [Fact]
    public async Task ReadyRemovalLeaveAndHostMigrationAreServerAuthoritativeAndRemovedMembershipIsRevoked()
    {
        using var factory = new WordGameApiFactory();
        using var host = await Guest(factory);
        using var bob = await Guest(factory);
        using var carol = await Guest(factory);
        var room = await Create(host);
        await Join(bob, room, "Bob"); await Join(carol, room, "Carol");
        using var hostEvents = await Events(host, room.Id);
        using var bobEvents = await Events(bob, room.Id);
        using var carolEvents = await Events(carol, room.Id);
        using var denied = await bob.PostAsJsonAsync(Path(room.Id, "remove"), new RemoveLobbyPlayerRequest(room.HostPlayerId));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        await Ready(host, room.Id); await Ready(bob, room.Id); await Ready(carol, room.Id);
        var ready = await State(host, room.Id);
        Assert.True(ready.CanStart);
        Assert.All(ready.Players, player => { Assert.True(player.IsReady); Assert.True(player.IsConnected); });
        using var notReady = await bob.PostAsJsonAsync(Path(room.Id, "ready"), new SetLobbyReadyRequest(ready.Version, false));
        Assert.Equal(HttpStatusCode.OK, notReady.StatusCode);
        Assert.False((await State(host, room.Id)).CanStart);
        using var stale = await carol.PostAsJsonAsync(Path(room.Id, "ready"), new SetLobbyReadyRequest(ready.Version, true));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var bobId = ready.Players.Single(player => player.DisplayName == "Bob").Id;
        using var kicked = await host.PostAsJsonAsync(Path(room.Id, "remove"), new RemoveLobbyPlayerRequest(bobId));
        Assert.Equal(HttpStatusCode.OK, kicked.StatusCode);
        var afterKick = await State(carol, room.Id);
        Assert.Equal(2, afterKick.Players.Count);
        Assert.All(afterKick.Players, player => Assert.False(player.IsReady));
        using var hidden = await bob.GetAsync(Path(room.Id));
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        using var hiddenEvents = await bob.GetAsync(Path(room.Id, "events"));
        Assert.Equal(HttpStatusCode.NotFound, hiddenEvents.StatusCode);
        foreach (var action in new[] { "ready", "leave", "start" })
        {
            object request = action == "ready" ? new SetLobbyReadyRequest(afterKick.Version, true)
                : action == "start" ? new StartLobbyRequest(afterKick.Version) : new LeaveLobbyRequest();
            using var forbidden = await bob.PostAsJsonAsync(Path(room.Id, action), request);
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        }
        using var rejoin = await bob.PostAsJsonAsync("/api/v1/lobbies/join", new JoinLobbyRequest(room.LobbyCode, "Bob"));
        Assert.Equal(HttpStatusCode.Forbidden, rejoin.StatusCode);
        using var reader = new StreamReader(await bobEvents.Content.ReadAsStreamAsync());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var removed = false;
        while (await reader.ReadLineAsync(timeout.Token) is { } line)
        {
            if (line == "event: removed") { removed = true; break; }
        }

        Assert.True(removed);
        using var leave = await host.PostAsJsonAsync(Path(room.Id, "leave"), new LeaveLobbyRequest());
        Assert.Equal(HttpStatusCode.OK, leave.StatusCode);
        var migrated = await State(carol, room.Id);
        Assert.Equal(migrated.Players.Single().Id, migrated.HostPlayerId);
        using var oldHost = await host.PostAsJsonAsync(Path(room.Id, "start"), new StartLobbyRequest(migrated.Version));
        Assert.Equal(HttpStatusCode.Forbidden, oldHost.StatusCode);
        using var close = await carol.PostAsJsonAsync(Path(room.Id, "leave"), new LeaveLobbyRequest());
        Assert.Equal(ApiLobbyStatus.Closed, (await Read(close)).Status);
        Assert.Null(await factory.Services.GetRequiredService<ILobbySessionStore>().GetAsync(room.Id));
    }

    [Fact]
    public async Task MultipleTabsAndReconnectionRetainOneIdentityAndOnlyLastDisconnectClearsReadiness()
    {
        using var factory = new WordGameApiFactory();
        using var host = await Guest(factory); using var bob = await Guest(factory);
        var room = await Create(host); await Join(bob, room, "Bob");
        using var hostEvents = await Events(host, room.Id);
        using var first = await Events(bob, room.Id); using var second = await Events(bob, room.Id);
        await Ready(host, room.Id); await Ready(bob, room.Id);
        var before = await State(host, room.Id);
        var bobId = before.Players.Single(player => player.DisplayName == "Bob").Id;
        var store = factory.Services.GetRequiredService<ILobbySessionStore>();
        first.Dispose();
        await WaitFor(async () => (await store.GetAsync(room.Id))!.Roster.Members[bobId].Connections.Count == 1);
        Assert.True((await State(host, room.Id)).Players.Single(player => player.Id == bobId).IsReady);
        second.Dispose();
        await WaitFor(async () => !(await store.GetAsync(room.Id))!.Roster.Members[bobId].IsConnected);
        var disconnected = await State(host, room.Id);
        Assert.False(disconnected.CanStart);
        Assert.Equal(room.HostPlayerId, disconnected.HostPlayerId);
        using var blocked = await host.PostAsJsonAsync(Path(room.Id, "start"), new StartLobbyRequest(disconnected.Version));
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        using var reconnected = await Events(bob, room.Id);
        var recovered = await State(bob, room.Id);
        Assert.Equal(2, recovered.Players.Count);
        var player = recovered.Players.Single(member => member.Id == bobId);
        Assert.True(player.IsConnected); Assert.False(player.IsReady);
    }

    [Fact]
    public async Task ADisconnectDuringStartupAbandonsTheCreatedGameAndRestoresTheLobby()
    {
        using var root = new WordGameApiFactory();
        PausedCatalog? catalog = null;
        using var factory = root.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddSingleton<ICategoryProvider>(provider => catalog = new PausedCatalog(new JsonCategoryProvider(
                System.IO.Path.Combine(provider.GetRequiredService<IWebHostEnvironment>().ContentRootPath, "Data", "Categories"))))));
        using var host = await Guest(factory); using var bob = await Guest(factory);
        var room = await Create(host); await Join(bob, room, "Bob");
        using var hostEvents = await Events(host, room.Id); using var bobEvents = await Events(bob, room.Id);
        await Ready(host, room.Id); await Ready(bob, room.Id);
        var ready = await State(host, room.Id);
        var bobId = ready.Players.Single(player => player.DisplayName == "Bob").Id;
        catalog!.Block = true;
        var start = host.PostAsJsonAsync(Path(room.Id, "start"), new StartLobbyRequest(ready.Version));
        try
        {
            await catalog.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            using var leave = await bob.PostAsJsonAsync(Path(room.Id, "leave"), new LeaveLobbyRequest());
            Assert.Equal(HttpStatusCode.Conflict, leave.StatusCode);
            bobEvents.Dispose();
            var store = factory.Services.GetRequiredService<ILobbySessionStore>();
            await WaitFor(async () => !(await store.GetAsync(room.Id))!.Roster.Members[bobId].IsConnected);
            catalog.Release.TrySetResult();
            using var result = await start;
            Assert.Equal(HttpStatusCode.Conflict, result.StatusCode);
            var waiting = await State(host, room.Id);
            Assert.Equal(ApiLobbyStatus.Waiting, waiting.Status);
            Assert.Null(waiting.GameId);
            Assert.All(waiting.Players, player => Assert.False(player.IsReady));
            Assert.Empty(await factory.Services.GetRequiredService<IGameSessionStore>().GetActiveAsync());
        }
        finally { catalog.Release.TrySetResult(); using var pending = await start; }
    }

    private static string Path(Guid id, string? action = null) => $"/api/v1/lobbies/{id}" + (action is null ? "" : $"/{action}");
    private static async Task<HttpClient> Guest(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var response = await client.PostAsJsonAsync("/api/v1/guests", new CreateGuestRequest());
        response.EnsureSuccessStatusCode(); return client;
    }
    private static async Task<LobbyResponse> Create(HttpClient host)
    {
        using var response = await host.PostAsJsonAsync("/api/v1/lobbies", new CreateLobbyRequest(Guid.NewGuid(), "Host"));
        return await Read(response);
    }
    private static async Task Join(HttpClient guest, LobbyResponse room, string name)
    {
        using var response = await guest.PostAsJsonAsync("/api/v1/lobbies/join", new JoinLobbyRequest(room.LobbyCode, name));
        response.EnsureSuccessStatusCode();
    }
    private static async Task<LobbyResponse> State(HttpClient client, Guid id)
    {
        using var response = await client.GetAsync(Path(id)); return await Read(response);
    }
    private static async Task<LobbyResponse> Read(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<LobbyResponse>(Json))!;
    }
    private static async Task Ready(HttpClient client, Guid id)
    {
        var state = await State(client, id);
        using var response = await client.PostAsJsonAsync(Path(id, "ready"), new SetLobbyReadyRequest(state.Version, true));
        response.EnsureSuccessStatusCode();
    }
    private static async Task<HttpResponseMessage> Events(HttpClient client, Guid id)
    {
        var response = await client.GetAsync(Path(id, "events"), HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode(); return response;
    }
    private static async Task WaitFor(Func<Task<bool>> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!await condition())
        {
            await Task.Delay(20, timeout.Token);
        }
    }
    private sealed class PausedCatalog(ICategoryProvider inner) : ICategoryProvider
    {
        public bool Block { get; set; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<IReadOnlyList<Category>> GetCategoriesAsync(CategoryDifficulty? difficulty = null, CancellationToken cancellationToken = default)
        {
            if (Block) { Entered.TrySetResult(); await Release.Task.WaitAsync(cancellationToken); }
            return await inner.GetCategoriesAsync(difficulty, cancellationToken);
        }
        public Task<CategoryDefinition?> GetByIdAsync(string categoryId, CancellationToken cancellationToken = default) => inner.GetByIdAsync(categoryId, cancellationToken);
    }
}
