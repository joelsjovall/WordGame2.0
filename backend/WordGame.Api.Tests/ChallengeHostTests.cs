using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WordGame.Api.Tests.Fixtures;
using WordGame.Application.Games;
using WordGame.Application.Sessions;
using WordGame.Domain;

namespace WordGame.Api.Tests;

public sealed class ChallengeHostTests
{
    [Fact]
    public async Task IssueExampleSucceedsUsingTheConfiguredJsonProviderAndEngine()
    {
        using var factory = new WordGameApiFactory();
        var engine = factory.Services.GetRequiredService<GameEngine>();
        var (id, players) = await Start(engine, 5);
        var words = new[] { "Stockholm", "Paris", "Berlin", "Stockholm", "Madrid", "Rome" };
        GameEngineResult? result = null;
        for (var index = 0; index < words.Length; index++)
        {
            var version = (await engine.GetStateAsync(id)).State!.Version;
            result = await engine.SubmitWordAsync(id, players[0].Id, words[index], new(Guid.NewGuid(), version));
            Assert.True(result.Succeeded);
            Assert.Equal(index == 3 ? WordSubmissionStatus.Duplicate : WordSubmissionStatus.Accepted, result.SubmissionStatus);
        }
        Assert.Equal(ChallengeOutcome.Succeeded, result!.ChallengeResult!.Outcome);
        Assert.Equal(5, result.ChallengeResult.AcceptedWordCount);
        Assert.Equal(RoundStatus.Completed, result.State!.Game.CurrentRound!.Status);
        Assert.All(result.State.Game.Scores.Values, score => Assert.Equal(0, score.Points));
    }

    [Fact]
    public async Task HostedTimerResolvesWithoutSubmissionsEvenWhenCategoryTimeoutsAreDisabled()
    {
        using var factory = new WordGameApiFactory().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Gameplay:ChallengeDuration"] = "00:00:00.050",
                    ["Gameplay:TimeoutSweepInterval"] = "00:00:00.010",
                    ["Gameplay:CategorySelectionTimeoutEnabled"] = "false"
                })));
        var engine = factory.Services.GetRequiredService<GameEngine>();
        var store = factory.Services.GetRequiredService<IGameSessionStore>();
        var (id, _) = await Start(engine, 2);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await foreach (var change in store.WatchAsync(id, timeout.Token))
        {
            var result = await engine.GetStateAsync(change.SessionId, timeout.Token);
            if (result.State!.Game.CurrentRound!.Status != RoundStatus.Completed)
            {
                continue;
            }

            Assert.Equal(ChallengeOutcome.Failed, result.ChallengeResult!.Outcome);
            Assert.Equal(0, result.ChallengeResult.AcceptedWordCount);
            Assert.Equal(2, result.ChallengeResult.RequiredWordCount);
            Assert.Equal(TimeSpan.FromMilliseconds(50), result.ChallengeResult.Deadline - result.ChallengeResult.StartedAt);
            Assert.Empty(result.State.Game.CurrentRound.Challenge!.Submissions);
            break;
        }
    }

    private static async Task<(Guid Id, Player[] Players)> Start(GameEngine engine, int required)
    {
        var players = new[] { new Player(Guid.NewGuid(), "Alice"), new Player(Guid.NewGuid(), "Bob") };
        var created = await engine.CreateAsync(players);
        Assert.True(created.Succeeded);
        var id = created.State!.Id;
        Assert.True((await engine.InitializeAsync(id)).Succeeded);
        Assert.True((await engine.StartAsync(id)).Succeeded);
        Assert.True((await engine.SelectCategoryAsync(id, players[0].Id, "european-capitals",
            new(Guid.NewGuid(), (await engine.GetStateAsync(id)).State!.Version))).Succeeded);
        Assert.True((await engine.PlaceBidAsync(id, players[0].Id, required,
            new(Guid.NewGuid(), (await engine.GetStateAsync(id)).State!.Version))).Succeeded);
        Assert.True((await engine.CallBluffAsync(id, players[1].Id,
            new(Guid.NewGuid(), (await engine.GetStateAsync(id)).State!.Version))).Succeeded);
        return (id, players);
    }
}
