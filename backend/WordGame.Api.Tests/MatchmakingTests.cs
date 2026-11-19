using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WordGame.Api.Contracts.V1;
using WordGame.Api.Tests.Fixtures;
using WordGame.Application.Games;
using WordGame.Application.Matchmaking;
using WordGame.Domain;

namespace WordGame.Api.Tests;

public sealed class MatchmakingTests
{
    private const string Searches = "/api/v1/matchmaking/searches";
    private const string Status = "/api/v1/matchmaking/status";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };

    [Fact]
    public async Task StatusCancellationExpiryAndRetentionUseAuthoritativeStates()
    {
        var clock = new ControlledGameplayClock();
        using var root = new WordGameApiFactory();
        using var factory = root.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<TimeProvider>(clock)));
        using var player = await Guest(factory);
        Assert.Equal(ApiMatchmakingStatus.Idle, (await State(player)).Status);
        var first = await Join(player);
        Assert.Equal(ApiMatchmakingStatus.Searching, first.Status);
        using var cancel = await player.DeleteAsync($"{Searches}/{first.SearchId}");
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
        Assert.Equal(ApiMatchmakingStatus.Cancelled, (await State(player)).Status);
        using var repeat = await player.DeleteAsync($"{Searches}/{first.SearchId}");
        Assert.Equal(HttpStatusCode.NoContent, repeat.StatusCode);
        var second = await Join(player);
        Assert.NotEqual(first.SearchId, second.SearchId);
        Assert.Equal(second.SearchId, (await State(player)).SearchId);
        clock.Advance(TimeSpan.FromMinutes(2));
        Assert.Equal(ApiMatchmakingStatus.Expired, (await State(player)).Status);
        clock.Advance(TimeSpan.FromMinutes(10));
        Assert.True(await factory.Services.GetRequiredService<MatchmakingService>().CleanupAsync() >= 1);
        using var gone = await player.GetAsync($"{Searches}/{second.SearchId}");
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
        Assert.Equal(ApiMatchmakingStatus.Idle, (await State(player)).Status);
    }

    [Fact]
    public async Task ConcurrentDuplicateJoinsProduceExactlyOneSearchPerPlayerAndDisjointPairs()
    {
        using var root = new WordGameApiFactory();
        using var factory = root.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["LobbyRateLimits:MatchmakingPermitsPerMinute"] = "1000" })));
        var clients = new List<HttpClient>();
        try
        {
            for (var index = 0; index < 20; index++)
            {
                clients.Add(await Guest(factory));
            }
            var results = await Task.WhenAll(clients.Select(async client =>
            {
                var joins = await Task.WhenAll(Join(client), Join(client));
                Assert.Equal(joins[0].SearchId, joins[1].SearchId);
                return await State(client);
            }));
            var final = await Task.WhenAll(clients.Select(State));
            Assert.Equal(20, results.Select(search => search.SearchId).Distinct().Count());
            Assert.All(final, search => { Assert.Equal(ApiMatchmakingStatus.Matched, search.Status); Assert.Null(search.GameId); });
            Assert.Equal(10, final.Select(search => search.MatchId).Distinct().Count());
            Assert.All(final.GroupBy(search => search.MatchId), pair => Assert.Equal(2, pair.Select(search => search.SearchId).Distinct().Count()));
            using var alreadyMatched = await clients[0].DeleteAsync($"{Searches}/{final[0].SearchId}");
            Assert.Equal(HttpStatusCode.Conflict, alreadyMatched.StatusCode);
        }
        finally
        {
            foreach (var client in clients) { client.Dispose(); }
        }
    }

    [Fact]
    public async Task PolicyCanDelaySelectionWithoutLosingFifoOrderOrChangingQueueOperations()
    {
        var policy = new PausedPolicy();
        using var root = new WordGameApiFactory();
        using var factory = root.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<IMatchmakingPolicy>(policy)));
        using var a = await Guest(factory); using var b = await Guest(factory); using var c = await Guest(factory); using var d = await Guest(factory);
        var first = await Join(a); var second = await Join(b); var third = await Join(c);
        Assert.Equal(ApiMatchmakingStatus.Searching, third.Status);
        policy.Enabled = true;
        await Join(d);
        var one = await State(a); var two = await State(b); var three = await State(c); var four = await State(d);
        Assert.Equal(first.SearchId, one.SearchId);
        Assert.Equal(second.SearchId, two.SearchId);
        Assert.Equal(one.MatchId, two.MatchId);
        Assert.Equal(three.MatchId, four.MatchId);
        Assert.NotEqual(one.MatchId, three.MatchId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PlayersInActivePartyOrOnlineGamesCannotJoinAndAreRemovedFromPendingSearches(bool online)
    {
        using var factory = new WordGameApiFactory();
        using var a = await Guest(factory); using var b = await Guest(factory);
        var queued = await Join(a);
        using var identityResponse = await a.PostAsJsonAsync("/api/v1/guests", new CreateGuestRequest());
        var identity = (await identityResponse.Content.ReadFromJsonAsync<GuestIdentityResponse>())!;
        var engine = factory.Services.GetRequiredService<GameEngine>();
        var created = await engine.CreateAsync([new Player(identity.PlayerId, "Player"), new Player(Guid.NewGuid(), "Other")],
            online ? MatchSettings.Online() : MatchSettings.Party(1));
        Assert.True((await engine.InitializeAsync(created.State!.Id)).Succeeded);
        Assert.True((await engine.StartAsync(created.State.Id)).Succeeded);
        using var denied = await a.PostAsJsonAsync(Searches, new JoinMatchmakingRequest("Player"));
        Assert.Equal(HttpStatusCode.Conflict, denied.StatusCode);
        var waiting = await Join(b);
        Assert.Equal(ApiMatchmakingStatus.Searching, waiting.Status);
        Assert.Equal(ApiMatchmakingStatus.Expired, (await State(a)).Status);
        Assert.Equal(queued.SearchId, (await State(a)).SearchId);
    }

    [Fact]
    public async Task RevokedSessionsAndExpiredReservationsNeverRemainEligible()
    {
        var clock = new ControlledGameplayClock();
        using var root = new WordGameApiFactory();
        using var factory = root.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<TimeProvider>(clock)));
        using var a = await Guest(factory); using var b = await Guest(factory); using var c = await Guest(factory);
        await Join(a);
        using var reset = await a.PostAsJsonAsync("/api/v1/guests/reset", new CreateGuestRequest());
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        using var denied = await a.GetAsync(Status);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        Assert.Equal(ApiMatchmakingStatus.Searching, (await Join(b)).Status);
        Assert.Equal(ApiMatchmakingStatus.Matched, (await Join(c)).Status);
        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(ApiMatchmakingStatus.Expired, (await State(b)).Status);
        Assert.Equal(ApiMatchmakingStatus.Expired, (await State(c)).Status);
    }

    [Fact]
    public async Task SearchesRequireIdentityOwnershipValidNamesAndSameOriginWrites()
    {
        using var factory = new WordGameApiFactory();
        using var anonymous = factory.CreateClient(); using var a = await Guest(factory); using var b = await Guest(factory);
        using var deniedJoin = await anonymous.PostAsJsonAsync(Searches, new JoinMatchmakingRequest("Player"));
        using var deniedStatus = await anonymous.GetAsync(Status);
        using var deniedCancel = await anonymous.DeleteAsync($"{Searches}/{Guid.NewGuid()}");
        Assert.All(new[] { deniedJoin, deniedStatus, deniedCancel }, response => Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode));
        foreach (var name in new[] { " ", new string('x', 33), "bad\u0001name" })
        {
            using var invalid = await a.PostAsJsonAsync(Searches, new JoinMatchmakingRequest(name));
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        }
        using var spoofed = await a.PostAsJsonAsync(Searches, new { displayName = "Player", playerId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.BadRequest, spoofed.StatusCode);
        var search = await Join(a);
        using var hidden = await b.GetAsync($"{Searches}/{search.SearchId}");
        using var forbiddenCancel = await b.DeleteAsync($"{Searches}/{search.SearchId}");
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, forbiddenCancel.StatusCode);
        a.DefaultRequestHeaders.Add("Origin", "https://foreign.example");
        using var foreign = await a.DeleteAsync($"{Searches}/{search.SearchId}");
        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
    }

    [Fact]
    public async Task CancellationRacingAnOpponentJoinHasOneConsistentOutcome()
    {
        using var factory = new WordGameApiFactory();
        using var a = await Guest(factory); using var b = await Guest(factory);
        var first = await Join(a);
        var cancellation = a.DeleteAsync($"{Searches}/{first.SearchId}");
        var joining = Join(b);
        using var cancelled = await cancellation;
        var second = await joining;
        var current = await State(a);
        if (cancelled.StatusCode == HttpStatusCode.NoContent)
        {
            Assert.Equal(ApiMatchmakingStatus.Cancelled, current.Status);
            Assert.Equal(ApiMatchmakingStatus.Searching, second.Status);
        }
        else
        {
            Assert.Equal(HttpStatusCode.Conflict, cancelled.StatusCode);
            Assert.Equal(ApiMatchmakingStatus.Matched, current.Status);
            Assert.Equal(current.MatchId, second.MatchId);
        }
    }

    private static async Task<HttpClient> Guest(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var response = await client.PostAsJsonAsync("/api/v1/guests", new CreateGuestRequest());
        response.EnsureSuccessStatusCode(); return client;
    }
    private static async Task<MatchmakingResponse> Join(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(Searches, new JoinMatchmakingRequest("Player"));
        return await Read(response);
    }
    private static async Task<MatchmakingResponse> State(HttpClient client)
    {
        using var response = await client.GetAsync(Status);
        Assert.True(response.Headers.CacheControl!.NoStore);
        return await Read(response);
    }
    private static async Task<MatchmakingResponse> Read(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<MatchmakingResponse>(Json))!;
    }
    private sealed class PausedPolicy : IMatchmakingPolicy
    {
        public bool Enabled { get; set; }
        public IReadOnlyList<MatchmakingSearch> SelectGroup(IReadOnlyList<MatchmakingSearch> waiting) =>
            Enabled ? new FifoDuelPolicy().SelectGroup(waiting) : [];
    }
}
