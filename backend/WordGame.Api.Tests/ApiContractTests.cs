using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WordGame.Api.Contracts.V1;
using WordGame.Api.Mapping;
using WordGame.Api.Tests.Fixtures;
using WordGame.Application.Sessions;
using WordGame.Domain;

namespace WordGame.Api.Tests;

public sealed class ApiContractTests
{
    [Fact]
    public async Task CategoryEndpointsReturnVersionedMetadataWithoutAnswerData()
    {
        using var factory = new WordGameApiFactory("Production");
        using var client = factory.CreateClient();
        using var list = await client.GetAsync("/api/v1/categories");
        using var listJson = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.NotEmpty(listJson.RootElement.GetProperty("items").EnumerateArray());
        using var detail = await client.GetAsync("/api/v1/categories/european-capitals");
        using var detailJson = JsonDocument.Parse(await detail.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        Assert.Equal("easy", detailJson.RootElement.GetProperty("difficulty").GetString());
        Assert.False(detailJson.RootElement.TryGetProperty("answers", out _));
        Assert.False(detailJson.RootElement.TryGetProperty("aliases", out _));
        using var filtered = await client.GetAsync("/api/v1/categories?difficulty=hard");
        using var filteredJson = JsonDocument.Parse(await filtered.Content.ReadAsStringAsync());
        var items = filteredJson.RootElement.GetProperty("items").EnumerateArray().ToArray();
        Assert.NotEmpty(items);
        Assert.All(items, category => Assert.Equal("hard", category.GetProperty("difficulty").GetString()));
    }

    [Theory]
    [InlineData("difficulty=expert")]
    [InlineData("difficulty=0")]
    [InlineData("difficulty=")]
    [InlineData("difficulty=easy&difficulty=hard")]
    public async Task InvalidCategoryFiltersUseStandardValidationProblemDetails(string query)
    {
        using var factory = new WordGameApiFactory("Production");
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/v1/categories?" + query);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(400, problem.RootElement.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(problem.RootElement.GetProperty("traceId").GetString()));
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty("difficulty", out _));
    }

    [Theory]
    [InlineData("/api/v1/categories/missing", 404)]
    [InlineData("/api/v2/categories", 404)]
    [InlineData("/api/v1/games", 404)]
    [InlineData("/api/v1/matchmaking/queue", 404)]
    public async Task MissingResourcesAndUnimplementedRoutesUseProblemDetails(string path, int status)
    {
        using var factory = new WordGameApiFactory("Production");
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(path);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(path, problem.RootElement.GetProperty("instance").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.RootElement.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task OpenApiDocumentsOnlyRealRoutesAndContainsFutureContractSchemas()
    {
        using var factory = new WordGameApiFactory("Development");
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var paths = document.RootElement.GetProperty("paths");
        Assert.Equal(25, paths.EnumerateObject().Count());
        Assert.True(paths.TryGetProperty("/api/v1/players/me/statistics", out _));
        Assert.True(paths.TryGetProperty("/api/v1/players/me/history", out _));
        Assert.True(paths.TryGetProperty("/api/v1/matchmaking/events", out _));
        Assert.True(paths.TryGetProperty("/api/v1/matchmaking/searches/{searchId}/accept", out _));
        Assert.True(paths.TryGetProperty("/api/v1/matchmaking/searches/{searchId}/decline", out _));
        Assert.True(paths.TryGetProperty("/api/v1/matchmaking/searches", out _));
        Assert.True(paths.TryGetProperty("/api/v1/matchmaking/searches/{searchId}", out _));
        Assert.True(paths.TryGetProperty("/api/v1/matchmaking/status", out _));
        Assert.True(paths.TryGetProperty("/api/v1/guests/reset", out _));
        Assert.True(paths.TryGetProperty("/api/v1/lobbies/join", out _));
        Assert.True(paths.TryGetProperty("/api/v1/guests", out _));
        Assert.True(paths.TryGetProperty("/api/v1/lobbies", out _));
        Assert.True(paths.TryGetProperty("/api/v1/games/{gameId}/state", out _));
        Assert.True(paths.TryGetProperty("/api/v1/games/{gameId}/events", out _));
        Assert.True(paths.TryGetProperty("/api/v1/categories", out _));
        Assert.False(paths.TryGetProperty("/api/v1/games", out _));
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        foreach (var type in new[] { "StartLobbyRequest", "JoinLobbyRequest", "GameStateResponse", "GameActionRequest",
            "GameActionResponse", "GameEventResponse", "ApiProblemDetails" })
        {
            Assert.True(schemas.TryGetProperty(type, out _), type);
        }
        var action = schemas.GetProperty("GameActionRequest");
        Assert.Equal(6, action.GetProperty("anyOf").GetArrayLength());
        Assert.All(action.GetProperty("anyOf").EnumerateArray(),
            variant =>
            {
                var component = variant.TryGetProperty("$ref", out var reference)
                    ? schemas.GetProperty(reference.GetString()!.Split('/').Last()) : variant;
                Assert.Contains(component.GetProperty("required").EnumerateArray(), item => item.GetString() == "type");
            });
        Assert.Contains(schemas.GetProperty("GameStateResponse").GetProperty("properties")
            .GetProperty("round").GetProperty("oneOf").EnumerateArray(), item => item.TryGetProperty("type", out var type) && type.GetString() == "null");
        var roundProperties = schemas.GetProperty("GameRoundResponse").GetProperty("properties");
        Assert.Contains(roundProperties.GetProperty("result").GetProperty("oneOf").EnumerateArray(),
            item => item.TryGetProperty("type", out var type) && type.GetString() == "null");
        Assert.Contains(roundProperties.GetProperty("categorySelectionDeadline").GetProperty("type").EnumerateArray(), item => item.GetString() == "null");
        var resultSchema = schemas.GetProperty("RoundResultResponse");
        Assert.True(resultSchema.GetProperty("properties").TryGetProperty("winnerId", out _));
        Assert.True(resultSchema.GetProperty("properties").TryGetProperty("scores", out _));
        var matchResultSchema = schemas.GetProperty("MatchResultResponse");
        Assert.Contains(schemas.GetProperty("GameStateResponse").GetProperty("properties").GetProperty("result").GetProperty("oneOf").EnumerateArray(),
            item => item.TryGetProperty("type", out var type) && type.GetString() == "null");
        Assert.True(matchResultSchema.GetProperty("properties").TryGetProperty("winnerIds", out _));
        Assert.True(matchResultSchema.GetProperty("properties").TryGetProperty("standings", out _));
        Assert.Equal("string", schemas.GetProperty("GameStateResponse").GetProperty("properties")
            .GetProperty("version").GetProperty("type").GetString());
        AssertLocalReferencesResolve(document.RootElement, schemas);
    }

    [Fact]
    public async Task OpenApiIsNotExposedByDefaultInProduction()
    {
        using var factory = new WordGameApiFactory("Production");
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UnsupportedCategoryMethodsAlsoUseProblemDetails()
    {
        using var factory = new WordGameApiFactory("Production");
        using var client = factory.CreateClient();
        using var response = await client.PostAsync("/api/v1/categories", null);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Equal(405, problem.RootElement.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(problem.RootElement.GetProperty("traceId").GetString()));
    }

    [Fact]
    public void ActionContractsSerializeAsDiscriminatedRequestsAndRejectUnsafeExtraFields()
    {
        using var factory = new WordGameApiFactory("Development");
        var options = factory.Services.GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions;
        GameActionRequest action = new PlaceBidAction(Guid.NewGuid(), "3", 5);
        var json = JsonSerializer.Serialize(action, options);
        using var body = JsonDocument.Parse(json);
        Assert.Equal("placeBid", body.RootElement.GetProperty("type").GetString());
        Assert.Equal("3", body.RootElement.GetProperty("expectedVersion").GetString());
        Assert.IsType<PlaceBidAction>(JsonSerializer.Deserialize<GameActionRequest>(json, options));
        var spoofed = json.Replace("\"wordCount\":5", "\"wordCount\":5,\"playerId\":\"spoofed\"");
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<GameActionRequest>(spoofed, options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<GameActionRequest>(
            json.Replace("\"placeBid\"", "\"awardPoints\""), options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<GameActionRequest>(
            json.Replace("\"expectedVersion\":\"3\"", "\"expectedVersion\":3"), options));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public async Task GameStateProjectionUsesTheSamePublicContractForBothRosterSizes(int count)
    {
        using var factory = new WordGameApiFactory("Production");
        var store = factory.Services.GetRequiredService<IGameSessionStore>();
        var players = Enumerable.Range(0, count).Select(index => new Player(Guid.NewGuid(), $"Player {index}")).ToArray();
        var created = await store.CreateAsync(players);
        var waiting = GameStateMapper.Map(created, DateTimeOffset.UtcNow);
        Assert.Null(waiting.Round);
        var active = await store.UpdateAsync(created.Id, session =>
        {
            session.BeginStarting();
            session.Game.StartRound(Guid.NewGuid(), players[0].Id);
            session.Activate();
            session.Connect(players[0].Id, Guid.NewGuid());
        });
        var mapped = GameStateMapper.Map(active with { Version = long.MaxValue }, DateTimeOffset.UtcNow);
        Assert.Equal("9223372036854775807", mapped.Version);
        Assert.Equal(count, mapped.Players.Count);
        Assert.Equal(ApiSessionStatus.Active, mapped.Status);
        Assert.True(mapped.Players[0].IsConnected);
        Assert.NotNull(mapped.Round);
        Assert.Null(mapped.Round.HighestBid);
        Assert.Null(mapped.Round.Challenge);
        var options = factory.Services.GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions;
        using var serialized = JsonDocument.Parse(JsonSerializer.Serialize(mapped, options));
        Assert.Equal("active", serialized.RootElement.GetProperty("status").GetString());
        Assert.False(serialized.RootElement.TryGetProperty("game", out _));
        Assert.Equal(GameStatus.InProgress, (await store.GetAsync(created.Id))!.Game.Status);

        var challenged = await store.UpdateAsync(created.Id, session =>
        {
            var round = session.Game.CurrentRound!;
            round.SelectCategory(players[0].Id, new Category("cars", "Cars", CategoryDifficulty.Easy));
            round.RecordBid(new Bid(players[0].Id, 2));
            round.AssignTurn(players[1].Id);
            var start = session.UpdatedAt;
            var challenge = new Challenge(Guid.NewGuid(), round.HighestBid!, players[1].Id, start, start.AddSeconds(30));
            round.BeginChallenge(challenge);
            challenge.RecordSubmission(new SubmittedWord(players[0].Id, "Volvo", start,
                WordSubmissionStatus.Accepted, "private-canonical-answer-id"));
        });
        var publicState = GameStateMapper.Map(challenged, DateTimeOffset.UtcNow);
        Assert.Equal(1, publicState.Round!.Challenge!.AcceptedWordCount);
        Assert.Equal(2, publicState.Round.HighestBid!.WordCount);
        Assert.Equal("Volvo", Assert.Single(publicState.Round.Challenge.Submissions).Text);
        var publicJson = JsonSerializer.Serialize(publicState, options);
        Assert.DoesNotContain("private-canonical-answer-id", publicJson);
        Assert.DoesNotContain("\"aliases\"", publicJson);
        Assert.DoesNotContain("\"answers\"", publicJson);
    }

    private static void AssertLocalReferencesResolve(JsonElement element, JsonElement schemas)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Name == "$ref" && property.Value.GetString() is { } reference &&
                    reference.StartsWith("#/components/schemas/", StringComparison.Ordinal))
                {
                    Assert.True(schemas.TryGetProperty(reference["#/components/schemas/".Length..], out _), reference);
                }
                AssertLocalReferencesResolve(property.Value, schemas);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                AssertLocalReferencesResolve(item, schemas);
            }
        }
    }
}
