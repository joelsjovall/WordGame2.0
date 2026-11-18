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

public sealed class RoundResultContractTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public async Task CurrentAndHistoricalResultsUseTheSamePublicProjectionWithoutPrivateAnswerData(int count)
    {
        using var factory = new WordGameApiFactory();
        var engine = factory.Services.GetRequiredService<GameEngine>();
        var players = Enumerable.Range(0, count).Select(index => new Player(Guid.NewGuid(), $"Player {index}")).ToArray();
        var created = await engine.CreateAsync(players);
        var id = created.State!.Id;
        Assert.True((await engine.InitializeAsync(id)).Succeeded);
        Assert.True((await engine.StartAsync(id)).Succeeded);
        async Task<PlayerActionContext> Context() => new(Guid.NewGuid(), (await engine.GetStateAsync(id)).State!.Version);
        Assert.True((await engine.SelectCategoryAsync(id, players[0].Id, "european-capitals", await Context())).Succeeded);
        Assert.True((await engine.PlaceBidAsync(id, players[0].Id, 2, await Context())).Succeeded);
        Assert.True((await engine.CallBluffAsync(id, players[1].Id, await Context())).Succeeded);
        foreach (var word in new[] { "Paris", "paris", "Unknown", "Berlin" })
        {
            Assert.True((await engine.SubmitWordAsync(id, players[0].Id, word, await Context())).Succeeded);
        }
        var completed = await engine.GetStateAsync(id);
        var mapped = GameStateMapper.Map(completed.State!, DateTimeOffset.UtcNow);
        var result = Assert.IsType<RoundResultResponse>(mapped.Round!.Result);
        Assert.Equal(players[0].Id, result.WinnerId);
        Assert.Equal(ApiChallengeOutcome.Succeeded, result.Outcome);
        Assert.Equal(2, result.AcceptedWordCount);
        Assert.Equal(4, result.Submissions.Count);
        Assert.Equal(ApiSubmissionStatus.Duplicate, result.Submissions[1].Status);
        Assert.Equal(ApiSubmissionStatus.Rejected, result.Submissions[2].Status);
        Assert.Equal(count, result.Scores.Count);
        Assert.Equal(52, result.Scores[0].PointsAwarded);
        Assert.Equal(52, result.Scores[0].TotalPoints);
        var options = factory.Services.GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions;
        var resultJson = JsonSerializer.Serialize(result, options);
        Assert.DoesNotContain("canonicalAnswerId", resultJson);
        Assert.DoesNotContain("normalizedText", resultJson);
        Assert.DoesNotContain("\"answers\"", resultJson);
        Assert.DoesNotContain("\"aliases\"", resultJson);
        var next = await engine.NextRoundAsync(id, result.RoundId, completed.State!.Version);
        Assert.True(next.Succeeded);
        Assert.Null(GameStateMapper.Map(next.State!, DateTimeOffset.UtcNow).Round!.Result);
        var historic = await engine.GetRoundResultAsync(id, result.RoundId);
        Assert.Equal(resultJson, JsonSerializer.Serialize(GameStateMapper.MapResult(historic.RoundResult!), options));
    }
}
