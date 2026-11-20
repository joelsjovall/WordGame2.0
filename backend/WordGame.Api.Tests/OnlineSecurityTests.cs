using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WordGame.Api.Tests.Fixtures;
using WordGame.Application.Games;
using WordGame.Domain;

namespace WordGame.Api.Tests;

public sealed class OnlineSecurityTests
{
    [Theory]
    [InlineData("DELETE", "/api/v1/matchmaking/searches/")]
    [InlineData("POST", "/api/v1/matchmaking/searches/", "/accept")]
    [InlineData("POST", "/api/v1/matchmaking/searches/", "/decline")]
    [InlineData("POST", "/api/v1/games/", "/rematch")]
    public async Task MutationsShareIpBudgetAcrossIdentitiesTargetsAndForgedHeaders(string method, string prefix, string suffix = "")
    {
        using var root = new WordGameApiFactory(); using var factory = Limited(root, "MatchmakingPermitsPerMinute", 1);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var client = AuthenticatedPlayerClient.Create(factory, Guid.NewGuid());
            client.DefaultRequestHeaders.Add("X-Forwarded-For", $"192.0.2.{attempt}");
            using var request = new HttpRequestMessage(new HttpMethod(method), $"{prefix}{Guid.NewGuid()}{suffix}");
            if (method == "POST")
            {
                request.Content = JsonContent.Create(suffix == "/rematch" ? (object)new { action = "request" } : new { });
            }

            using var response = await client.SendAsync(request);
            Assert.Equal(attempt == 0 ? HttpStatusCode.NotFound : HttpStatusCode.TooManyRequests, response.StatusCode);
            if (attempt == 1)
            {
                Assert.True(response.Headers.RetryAfter!.Delta > TimeSpan.Zero);
            }
        }
    }

    [Fact]
    public async Task JoinAndCancelUseOneBudgetButStatusReadsUseASeparateBudget()
    {
        using var root = new WordGameApiFactory(); using var factory = Limited(root, "MatchmakingPermitsPerMinute", 2);
        using var client = AuthenticatedPlayerClient.Create(factory, Guid.NewGuid());
        using var join = await client.PostAsJsonAsync("/api/v1/matchmaking/searches", new { displayName = "Player" });
        join.EnsureSuccessStatusCode();
        var id = (await join.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("searchId").GetString();
        using var cancel = await client.DeleteAsync($"/api/v1/matchmaking/searches/{id}");
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
        using var excessive = await client.PostAsJsonAsync("/api/v1/matchmaking/searches", new { displayName = "Player" });
        Assert.Equal(HttpStatusCode.TooManyRequests, excessive.StatusCode);
        using var status = await client.GetAsync("/api/v1/matchmaking/status"); status.EnsureSuccessStatusCode();
    }

    [Theory]
    [InlineData("/api/v1/matchmaking/status")]
    [InlineData("/api/v1/matchmaking/events")]
    [InlineData("/api/v1/matchmaking/searches/00000000-0000-0000-0000-000000000001")]
    [InlineData("/api/v1/games/00000000-0000-0000-0000-000000000001/rematch")]
    public async Task ReadsAndStreamOpenAttemptsAreLimited(string path)
    {
        using var root = new WordGameApiFactory(); using var factory = Limited(root, "MatchmakingReadPermitsPerMinute", 1);
        using var client = AuthenticatedPlayerClient.Create(factory, Guid.NewGuid());
        using var first = await client.GetAsync(path, HttpCompletionOption.ResponseHeadersRead);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, first.StatusCode);
        using var second = await client.GetAsync(path, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
    }

    [Fact]
    public async Task GameplayBudgetAndAuditLoggingContainNoPlayerInputOrCredentials()
    {
        var log = new SecurityLog();
        using var root = new WordGameApiFactory();
        using var factory = Limited(root, "GameplayPermitsPerMinute", 1).WithWebHostBuilder(builder =>
            builder.ConfigureLogging(logging => logging.AddProvider(log)));
        using var client = AuthenticatedPlayerClient.Create(factory, Guid.NewGuid());
        var path = $"/api/v1/games/{Guid.NewGuid()}/actions";
        var marker = "PRIVATE_PLAYER_INPUT";
        using var first = await client.PostAsJsonAsync(path, new { type = "submitWord", actionId = Guid.NewGuid(), expectedVersion = "0", word = marker });
        Assert.Equal(HttpStatusCode.NotFound, first.StatusCode);
        using var second = await client.PostAsJsonAsync(path, new { type = "submitWord", actionId = Guid.NewGuid(), expectedVersion = "0", word = marker });
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
        Assert.Contains(log.Messages, message => message.Contains("SubmitGameAction", StringComparison.Ordinal));
        Assert.All(log.Messages, message =>
        {
            Assert.DoesNotContain(marker, message);
            Assert.DoesNotContain("wordgame.player", message);
            Assert.DoesNotContain("Cookie", message);
        });
    }

    [Fact]
    public async Task AnonymousForgedAndExpiredSessionsCannotUseOnlineOperations()
    {
        using var factory = new WordGameApiFactory();
        using var anonymous = factory.CreateClient(); using var forged = factory.CreateClient();
        forged.DefaultRequestHeaders.Add("Cookie", "wordgame.player=forged");
        forged.DefaultRequestHeaders.Add("X-Player-Id", Guid.NewGuid().ToString());
        using var expired = AuthenticatedPlayerClient.Create(factory, Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-1));
        foreach (var client in new[] { anonymous, forged, expired })
        {
            foreach (var path in new[] { "/api/v1/matchmaking/status", "/api/v1/matchmaking/events", $"/api/v1/games/{Guid.NewGuid()}/rematch" })
            {
                using var response = await client.GetAsync(path); Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            }
            using var join = await client.PostAsJsonAsync("/api/v1/matchmaking/searches", new { displayName = "Player" });
            using var cancel = await client.DeleteAsync($"/api/v1/matchmaking/searches/{Guid.NewGuid()}");
            using var accept = await client.PostAsJsonAsync($"/api/v1/matchmaking/searches/{Guid.NewGuid()}/accept", new { });
            using var decline = await client.PostAsJsonAsync($"/api/v1/matchmaking/searches/{Guid.NewGuid()}/decline", new { });
            using var rematch = await client.PostAsJsonAsync($"/api/v1/games/{Guid.NewGuid()}/rematch", new { action = "request" });
            Assert.All(new[] { join, cancel, accept, decline, rematch }, response => Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode));
        }
    }

    [Fact]
    public async Task NonownersCannotReadCancelAcceptOrDeclineAnotherSearch()
    {
        using var factory = new WordGameApiFactory();
        using var owner = AuthenticatedPlayerClient.Create(factory, Guid.NewGuid());
        using var stranger = AuthenticatedPlayerClient.Create(factory, Guid.NewGuid());
        using var join = await owner.PostAsJsonAsync("/api/v1/matchmaking/searches", new { displayName = "Owner" }); join.EnsureSuccessStatusCode();
        var payload = await join.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(payload);
        var id = document.RootElement.GetProperty("searchId").GetString();
        var path = $"/api/v1/matchmaking/searches/{id}";
        using var read = await stranger.GetAsync(path); using var cancel = await stranger.DeleteAsync(path);
        using var accept = await stranger.PostAsJsonAsync(path + "/accept", new { });
        using var decline = await stranger.PostAsJsonAsync(path + "/decline", new { });
        Assert.All(new[] { read, cancel, accept, decline }, response => Assert.Equal(HttpStatusCode.NotFound, response.StatusCode));
        using var stillOwned = await owner.GetAsync(path); stillOwned.EnsureSuccessStatusCode();
        Assert.Equal("searching", (await stillOwned.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        foreach (var sensitive in new[] { "credentialSessionId", "sessionId", "revoked", "cookie", "password" })
        {
            Assert.DoesNotContain(sensitive, payload, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task GameInputsCannotForgeScoresActorsResultsOrReplayActions()
    {
        using var factory = new WordGameApiFactory();
        var actor = Guid.NewGuid(); var opponent = Guid.NewGuid();
        using var owner = AuthenticatedPlayerClient.Create(factory, actor);
        using var stranger = AuthenticatedPlayerClient.Create(factory, Guid.NewGuid());
        var engine = factory.Services.GetRequiredService<GameEngine>();
        var created = await engine.CreateAsync([new Player(actor, "Alice"), new Player(opponent, "Bob")], MatchSettings.Online());
        var id = created.State!.Id;
        await engine.InitializeAsync(id); await engine.StartAsync(id);
        var before = (await engine.GetStateAsync(id)).State!;
        var path = $"/api/v1/games/{id}/actions";
        using var denied = await stranger.PostAsJsonAsync(path, new { type = "forfeitMatch", actionId = Guid.NewGuid(), expectedVersion = before.Version.ToString() });
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        foreach (var body in new object[]
        {
            new { type = "completeMatch", actionId = Guid.NewGuid(), expectedVersion = before.Version.ToString() },
            new { type = "placeBid", actionId = Guid.NewGuid(), expectedVersion = before.Version.ToString(), wordCount = 1, score = 999 },
            new { type = "forfeitMatch", actionId = Guid.NewGuid(), expectedVersion = before.Version.ToString(), playerId = opponent },
            new { type = "submitWord", actionId = Guid.NewGuid(), expectedVersion = before.Version.ToString(), word = new string('x', 257) },
            new { type = "selectCategory", actionId = Guid.NewGuid(), expectedVersion = before.Version.ToString(), categoryId = new string('x', 129) }
        })
        {
            using var rejected = await owner.PostAsJsonAsync(path, body); Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        }
        Assert.Equal(before.Version, (await engine.GetStateAsync(id)).State!.Version);
        var actionId = Guid.NewGuid();
        using var select = await owner.PostAsJsonAsync(path, new { type = "selectCategory", actionId, expectedVersion = before.Version.ToString(), categoryId = "european-capitals" });
        select.EnsureSuccessStatusCode();
        var after = (await engine.GetStateAsync(id)).State!;
        using var replay = await owner.PostAsJsonAsync(path, new { type = "selectCategory", actionId, expectedVersion = after.Version.ToString(), categoryId = "european-capitals" });
        Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode);
        Assert.Equal(after.Version, (await engine.GetStateAsync(id)).State!.Version);
        Assert.All(after.Game.Scores.Values, score => Assert.Equal(0, score.Points));
        Assert.Null(after.Game.Result);
    }

    private static WebApplicationFactory<Program> Limited(WordGameApiFactory root, string setting, int permits) =>
        root.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
            new Dictionary<string, string?> { [$"LobbyRateLimits:{setting}"] = permits.ToString() })));

    private sealed class SecurityLog : ILoggerProvider, ILogger
    {
        internal readonly ConcurrentBag<string> Messages = [];
        public ILogger CreateLogger(string categoryName) => categoryName == "WordGame.Security" ? this : Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
        public void Dispose() { }
    }
}
