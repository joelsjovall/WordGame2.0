using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using WordGame.Api.Contracts.V1;
using WordGame.Api.Tests.Fixtures;
using WordGame.Application.Games;
using WordGame.Application.Sessions;
using WordGame.Domain;

namespace WordGame.Api.Tests;

public sealed class OnlineStatisticsTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };
    [Fact]
    public async Task CompletionRecordsStatisticsOnceAndOnlyVerifiedOwnerCanReadThem()
    {
        using var factory = new WordGameApiFactory();
        using var alice = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var bob = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var stranger = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var first = await Guest(alice); var second = await Guest(bob); await Guest(stranger);
        var engine = factory.Services.GetRequiredService<GameEngine>();
        var created = await engine.CreateAsync([new Player(first, "Alice"), new Player(second, "Bob")], MatchSettings.Online());
        var id = created.State!.Id;
        Assert.True((await engine.InitializeAsync(id)).Succeeded);
        Assert.True((await engine.StartAsync(id)).Succeeded);
        var active = (await engine.GetStateAsync(id)).State!;
        Assert.True((await engine.ForfeitAsync(id, first, new(Guid.NewGuid(), active.Version))).Succeeded);
        var completed = (await engine.GetStateAsync(id)).State!;
        Assert.False((await engine.ForfeitAsync(id, second, new(Guid.NewGuid(), completed.Version))).Succeeded);
        // A later terminal presence update must not double-count the match either.
        Assert.True((await engine.ConnectAsync(id, second, Guid.NewGuid())).Succeeded);
        var losses = await alice.GetFromJsonAsync<OnlineStatisticsResponse>("/api/v1/players/me/statistics");
        var wins = await bob.GetFromJsonAsync<OnlineStatisticsResponse>("/api/v1/players/me/statistics");
        Assert.Equal(1, losses!.Losses); Assert.Equal(1, losses.MatchesPlayed);
        Assert.Equal(1, wins!.Wins); Assert.Equal(100, wins.WinPercentage);
        var history = await alice.GetFromJsonAsync<OnlineHistoryResponse>("/api/v1/players/me/history", Json);
        Assert.Equal(id, Assert.Single(history!.Items).MatchId);
        Assert.Equal(2, history.Items[0].Players.Count);
        Assert.Empty((await stranger.GetFromJsonAsync<OnlineHistoryResponse>("/api/v1/players/me/history", Json))!.Items);
        // Caller-controlled query values cannot select another player.
        Assert.Equal(0, (await stranger.GetFromJsonAsync<OnlineStatisticsResponse>($"/api/v1/players/me/statistics?playerId={first}"))!.MatchesPlayed);
        using var write = await bob.PostAsJsonAsync("/api/v1/players/me/statistics", new { wins = 999 });
        Assert.Equal(HttpStatusCode.MethodNotAllowed, write.StatusCode);
        using var reset = await alice.PostAsJsonAsync("/api/v1/guests/reset", new { });
        reset.EnsureSuccessStatusCode();
        using var denied = await alice.GetAsync("/api/v1/players/me/statistics");
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        Assert.NotEqual(first, await Guest(alice));
        Assert.Equal(0, (await alice.GetFromJsonAsync<OnlineStatisticsResponse>("/api/v1/players/me/statistics"))!.MatchesPlayed);
    }

    [Theory]
    [InlineData("statistics")]
    [InlineData("history")]
    public async Task AnonymousReadsAreRejected(string resource)
    {
        using var factory = new WordGameApiFactory(); using var client = factory.CreateClient();
        using var response = await client.GetAsync($"/api/v1/players/me/{resource}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<Guid> Guest(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/guests", new CreateGuestRequest());
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<GuestIdentityResponse>())!.PlayerId;
    }
}
