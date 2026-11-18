using System.Text.Json;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WordGame.Api.Contracts.V1;
using WordGame.Api.Mapping;
using WordGame.Api.Tests.Fixtures;
using WordGame.Application.Games;
using WordGame.Domain;

namespace WordGame.Api.Tests;

public sealed class MatchResultContractTests
{
    [Theory]
    [InlineData(2, false)]
    [InlineData(4, false)]
    [InlineData(2, true)]
    public async Task BothFormatsExposeFinalSettingsStandingsAndWinnersThroughSharedState(int count, bool online)
    {
        using var factory = new WordGameApiFactory();
        var engine = factory.Services.GetRequiredService<GameEngine>();
        var players = Enumerable.Range(0, count).Select(index => new Player(Guid.NewGuid(), $"Player {index}")).ToArray();
        var settings = online ? MatchSettings.Online() : MatchSettings.Party(1);
        var created = await engine.CreateAsync(players, settings);
        var id = created.State!.Id;
        var initial = GameStateMapper.Map(created.State, DateTimeOffset.UtcNow);
        Assert.Null(initial.Result);
        Assert.Equal(online ? ApiMatchFormat.Online : ApiMatchFormat.Party, initial.Match.Format);
        Assert.Equal(online ? 6 : 1, initial.Match.RoundCount);
        Assert.True((await engine.InitializeAsync(id)).Succeeded);
        Assert.True((await engine.StartAsync(id)).Succeeded);
        async Task<PlayerActionContext> Context() => new(Guid.NewGuid(), (await engine.GetStateAsync(id)).State!.Version);
        for (var round = 0; round < settings.RoundCount; round++)
        {
            var starter = round % count;
            Assert.True((await engine.SelectCategoryAsync(id, players[starter].Id, "european-capitals", await Context())).Succeeded);
            Assert.True((await engine.PlaceBidAsync(id, players[starter].Id, 1, await Context())).Succeeded);
            Assert.True((await engine.CallBluffAsync(id, players[(starter + 1) % count].Id, await Context())).Succeeded);
            var completion = await engine.SubmitWordAsync(id, players[starter].Id, "Paris", await Context());
            Assert.True(completion.Succeeded);
            if (round < settings.RoundCount - 1)
            {
                Assert.Null(completion.MatchResult);
                Assert.True((await engine.NextRoundAsync(id, completion.RoundResult!.RoundId, completion.State!.Version)).Succeeded);
            }
        }
        var state = await engine.GetStateAsync(id);
        var mapped = GameStateMapper.Map(state.State!, DateTimeOffset.UtcNow);
        Assert.Equal(ApiSessionStatus.Completed, mapped.Status);
        var result = Assert.IsType<MatchResultResponse>(mapped.Result);
        Assert.Equal(id, result.GameId);
        Assert.Equal(settings.RoundCount, result.CompletedRoundCount);
        Assert.Equal(count, result.Standings.Count);
        Assert.Equal(online, result.IsTie);
        Assert.Equal(online ? 2 : 1, result.WinnerIds.Count);
        foreach (var standing in result.Standings)
        {
            Assert.Equal(mapped.Players.Single(player => player.Id == standing.PlayerId).Score, standing.Score);
        }
        var options = factory.Services.GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions;
        var json = JsonSerializer.Serialize(mapped, options);
        Assert.Contains(online ? "\"format\":\"online\"" : "\"format\":\"party\"", json);
        Assert.DoesNotContain("canonicalAnswerId", json);
        Assert.DoesNotContain("\"answers\"", json);
        Assert.DoesNotContain("\"aliases\"", json);
        foreach (var participant in players)
        {
            Assert.Contains(mapped.Players, player => player.Id == participant.Id);
            Assert.Same(state.MatchResult, (await engine.GetStateAsync(id)).MatchResult);
        }
    }
}
