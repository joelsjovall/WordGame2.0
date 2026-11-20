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

public sealed partial class MatchmakingTests
{
    [Fact]
    public async Task MatchNotificationsRequireIdentityAndResetClosesTheStream()
    {
        using var factory = new WordGameApiFactory();
        using var anonymous = factory.CreateClient();
        using var denied = await anonymous.GetAsync("/api/v1/matchmaking/events");
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        using var player = await Guest(factory);
        using var events = await MatchEvents(player);
        using var reader = new StreamReader(await events.Content.ReadAsStreamAsync());
        await MatchEvent(reader, ApiMatchmakingStatus.Idle);
        using var reset = await player.PostAsJsonAsync("/api/v1/guests/reset", new CreateGuestRequest());
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        _ = await reader.ReadToEndAsync(timeout.Token);
    }

    [Fact]
    public async Task BothPlayersReceiveOneGameAcceptConcurrentlyAndEnterTheSharedEngine()
    {
        using var factory = new WordGameApiFactory();
        using var a = await Guest(factory); using var b = await Guest(factory);
        using var aEvents = await MatchEvents(a); using var bEvents = await MatchEvents(b);
        using var aReader = new StreamReader(await aEvents.Content.ReadAsStreamAsync());
        using var bReader = new StreamReader(await bEvents.Content.ReadAsStreamAsync());
        var first = await Join(a); var second = await Join(b);
        var aFound = await MatchEvent(aReader, ApiMatchmakingStatus.Matched);
        var bFound = await MatchEvent(bReader, ApiMatchmakingStatus.Matched);
        Assert.NotNull(aFound.GameId);
        Assert.Equal(second.GameId, aFound.GameId);
        Assert.Equal(aFound.GameId, bFound.GameId);
        var gameId = aFound.GameId!.Value;
        var engine = factory.Services.GetRequiredService<GameEngine>();
        var pending = (await engine.GetStateAsync(gameId)).State!;
        Assert.Equal(SessionStatus.Starting, pending.Status);
        Assert.Equal(MatchFormat.Online, pending.Game.Settings.Format);
        Assert.Equal(6, pending.Game.Settings.RoundCount);
        using var premature = await a.PostAsJsonAsync($"/api/v1/games/{gameId}/actions", new
        { type = "selectCategory", actionId = Guid.NewGuid(), expectedVersion = pending.Version.ToString(), categoryId = "european-capitals" });
        Assert.Equal(HttpStatusCode.Conflict, premature.StatusCode);
        var accepted = await Respond(a, first.SearchId, "accept");
        Assert.True(accepted.HasAccepted);
        Assert.False((await State(b)).HasAccepted);
        Assert.Equal(SessionStatus.Starting, (await engine.GetStateAsync(gameId)).State!.Status);
        var accepts = await Task.WhenAll(Respond(a, first.SearchId, "accept"), Respond(b, second.SearchId, "accept"), Respond(b, second.SearchId, "accept"));
        Assert.All(accepts, result => Assert.Equal(gameId, result.GameId));
        Assert.Equal(gameId, (await MatchEvent(aReader, ApiMatchmakingStatus.Ready)).GameId);
        Assert.Equal(gameId, (await MatchEvent(bReader, ApiMatchmakingStatus.Ready)).GameId);
        var active = (await engine.GetStateAsync(gameId)).State!;
        Assert.Equal(SessionStatus.Active, active.Status);
        Assert.Equal(active.Players[0].PlayerId, active.Game.CurrentRound!.Turn.PlayerId);
        Assert.Single(await factory.Services.GetRequiredService<IGameSessionStore>().GetOpenAsync());
        using var state = await b.GetAsync($"/api/v1/games/{gameId}/state");
        Assert.Equal(HttpStatusCode.OK, state.StatusCode);
        using var playable = await a.PostAsJsonAsync($"/api/v1/games/{gameId}/actions", new
        { type = "selectCategory", actionId = Guid.NewGuid(), expectedVersion = active.Version.ToString(), categoryId = "european-capitals" });
        Assert.Equal(HttpStatusCode.OK, playable.StatusCode);
        using var reconnected = await MatchEvents(a);
        using var recoveredReader = new StreamReader(await reconnected.Content.ReadAsStreamAsync());
        Assert.Equal(gameId, (await MatchEvent(recoveredReader, ApiMatchmakingStatus.Ready)).GameId);
    }

    [Fact]
    public async Task DeclineAfterOneAcceptanceCancelsBothAndAllowsAFreshSearch()
    {
        using var factory = new WordGameApiFactory();
        using var a = await Guest(factory); using var b = await Guest(factory); using var outsider = await Guest(factory);
        var first = await Join(a); var second = await Join(b);
        using var hidden = await outsider.PostAsJsonAsync($"{Searches}/{first.SearchId}/accept", new MatchAcceptanceRequest());
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        a.DefaultRequestHeaders.Add("Origin", "https://foreign.example");
        using var foreign = await a.PostAsJsonAsync($"{Searches}/{first.SearchId}/accept", new MatchAcceptanceRequest());
        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
        a.DefaultRequestHeaders.Remove("Origin");
        await Respond(a, first.SearchId, "accept");
        await Respond(b, second.SearchId, "decline");
        Assert.Equal(ApiMatchmakingStatus.Cancelled, (await State(a)).Status);
        Assert.Equal(ApiMatchmakingStatus.Cancelled, (await State(b)).Status);
        Assert.Equal(SessionStatus.Abandoned, (await factory.Services.GetRequiredService<GameEngine>().GetStateAsync(second.GameId!.Value)).State!.Status);
        await Join(a);
        var next = await Join(b);
        Assert.NotEqual(second.GameId, next.GameId);
    }

    [Fact]
    public async Task AcceptanceTimeoutNotifiesBothStreamsWithoutClientPolling()
    {
        var clock = new ControlledGameplayClock();
        using var root = new WordGameApiFactory();
        using var factory = root.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services => services.AddSingleton<TimeProvider>(clock));
            builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            { ["Matchmaking:AcceptanceTimeout"] = "00:00:01", ["Matchmaking:CleanupInterval"] = "00:00:00.050" }));
        });
        using var a = await Guest(factory); using var b = await Guest(factory);
        using var aEvents = await MatchEvents(a); using var bEvents = await MatchEvents(b);
        using var aReader = new StreamReader(await aEvents.Content.ReadAsStreamAsync());
        using var bReader = new StreamReader(await bEvents.Content.ReadAsStreamAsync());
        var first = await Join(a); var second = await Join(b);
        await MatchEvent(aReader, ApiMatchmakingStatus.Matched);
        await MatchEvent(bReader, ApiMatchmakingStatus.Matched);
        await Respond(a, first.SearchId, "accept");
        clock.Advance(TimeSpan.FromSeconds(2));
        await MatchEvent(aReader, ApiMatchmakingStatus.Expired);
        await MatchEvent(bReader, ApiMatchmakingStatus.Expired);
        using var late = await b.PostAsJsonAsync($"{Searches}/{second.SearchId}/accept", new MatchAcceptanceRequest());
        Assert.Equal(HttpStatusCode.Conflict, late.StatusCode);
        Assert.Equal(SessionStatus.Abandoned, (await factory.Services.GetRequiredService<GameEngine>().GetStateAsync(second.GameId!.Value)).State!.Status);
    }

    [Fact]
    public async Task PartyCreationRacingOnlinePairingCannotAssignAPlayerToTwoOpenGames()
    {
        using var factory = new WordGameApiFactory();
        using var a = await Guest(factory); using var b = await Guest(factory);
        using var identityResponse = await a.PostAsJsonAsync("/api/v1/guests", new CreateGuestRequest());
        var identity = (await identityResponse.Content.ReadFromJsonAsync<GuestIdentityResponse>())!;
        await Join(a);
        var engine = factory.Services.GetRequiredService<GameEngine>();
        var online = Join(b);
        var party = engine.CreateExclusiveAsync([new Player(identity.PlayerId, "Party"), new Player(Guid.NewGuid(), "Other")], MatchSettings.Party(1));
        var selected = await online;
        var partyResult = await party;
        Assert.Single(await factory.Services.GetRequiredService<IGameSessionStore>().GetOpenAsync());
        if (partyResult.Succeeded)
        {
            // Party may win before queue eligibility checks, or between those checks and exclusive creation.
            Assert.Contains(selected.Status, new[] { ApiMatchmakingStatus.Searching, ApiMatchmakingStatus.Failed });
            Assert.Null(selected.GameId);
        }
        else
        {
            Assert.Equal(ApiMatchmakingStatus.Matched, selected.Status);
            Assert.NotNull(selected.GameId);
        }
    }

    [Fact]
    public async Task AcceptRacingDeclineCommitsOneOutcome()
    {
        using var factory = new WordGameApiFactory();
        using var a = await Guest(factory); using var b = await Guest(factory);
        var first = await Join(a); var second = await Join(b);
        await Respond(a, first.SearchId, "accept");
        var accepting = b.PostAsJsonAsync($"{Searches}/{second.SearchId}/accept", new MatchAcceptanceRequest());
        var declining = a.PostAsJsonAsync($"{Searches}/{first.SearchId}/decline", new MatchAcceptanceRequest());
        using var accepted = await accepting; using var declined = await declining;
        Assert.Equal(1, new[] { accepted, declined }.Count(response => response.IsSuccessStatusCode));
        var stateA = await State(a); var stateB = await State(b);
        Assert.Equal(stateA.Status, stateB.Status);
        var game = (await factory.Services.GetRequiredService<GameEngine>().GetStateAsync(second.GameId!.Value)).State!;
        Assert.Equal(accepted.IsSuccessStatusCode ? SessionStatus.Active : SessionStatus.Abandoned, game.Status);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task InitializationOrStartFailureRollsBackTheGameAndSearchesCanRetry(int failAtUpdate)
    {
        using var root = new WordGameApiFactory();
        var faulty = new InitializationFailureStore(root.Services.GetRequiredService<IGameSessionStore>(), failAtUpdate);
        using var factory = root.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<IGameSessionStore>(faulty)));
        using var a = await Guest(factory); using var b = await Guest(factory);
        var first = await Join(a);
        var failed = await Join(b);
        if (failAtUpdate == 2)
        {
            await Respond(a, first.SearchId, "accept");
            failed = await Respond(b, failed.SearchId, "accept");
        }
        Assert.Equal(ApiMatchmakingStatus.Failed, failed.Status);
        Assert.Equal(ApiMatchmakingStatus.Failed, (await State(a)).Status);
        Assert.Empty(await faulty.GetOpenAsync());
        Assert.Equal(SessionStatus.Abandoned, (await faulty.GetAsync(faulty.CreatedId))!.Status);
        await Join(a);
        Assert.Equal(ApiMatchmakingStatus.Matched, (await Join(b)).Status);
    }

    private static async Task<MatchmakingResponse> Respond(HttpClient client, Guid? searchId, string action)
    {
        using var response = await client.PostAsJsonAsync($"{Searches}/{searchId}/{action}", new MatchAcceptanceRequest());
        return await Read(response);
    }
    private static async Task<HttpResponseMessage> MatchEvents(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/matchmaking/events", HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return response;
    }
    private static async Task<MatchmakingResponse> MatchEvent(StreamReader reader, ApiMatchmakingStatus expected)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (await reader.ReadLineAsync(timeout.Token) is { } line)
        {
            if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                var response = JsonSerializer.Deserialize<MatchmakingResponse>(line[5..], Json)!;
                if (response.Status == expected) { return response; }
            }
        }
        throw new InvalidOperationException("Matchmaking stream ended before the expected update.");
    }

    private sealed class InitializationFailureStore(IGameSessionStore inner, int failAtUpdate) : IGameSessionStore
    {
        private int updates;
        public Guid CreatedId { get; private set; }
        public async Task<SessionSnapshot> CreateAsync(IEnumerable<Player> players, CancellationToken cancellationToken = default, MatchSettings? settings = null)
        {
            var created = await inner.CreateAsync(players, cancellationToken, settings); CreatedId = created.Id; return created;
        }
        public Task<SessionSnapshot> UpdateAsync(Guid id, Action<GameSession> update, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref updates) == failAtUpdate) { throw new InvalidOperationException("Injected creation failure."); }
            return inner.UpdateAsync(id, update, cancellationToken);
        }
        public Task<SessionSnapshot> UpdateAsync(Guid id, long version, Action<GameSession> update, CancellationToken cancellationToken = default) => inner.UpdateAsync(id, version, update, cancellationToken);
        public Task<SessionSnapshot?> GetAsync(Guid id, CancellationToken cancellationToken = default) => inner.GetAsync(id, cancellationToken);
        public Task<IReadOnlyList<SessionSnapshot>> GetActiveAsync(CancellationToken cancellationToken = default) => inner.GetActiveAsync(cancellationToken);
        public Task<IReadOnlyList<SessionSnapshot>> GetOpenAsync(CancellationToken cancellationToken = default) => inner.GetOpenAsync(cancellationToken);
        public IAsyncEnumerable<SessionChange> WatchAsync(Guid id, CancellationToken cancellationToken = default) => inner.WatchAsync(id, cancellationToken);
        public Task<int> CleanupAsync(CancellationToken cancellationToken = default) => inner.CleanupAsync(cancellationToken);
    }
}
