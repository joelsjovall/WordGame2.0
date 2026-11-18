using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WordGame.Api.Tests.Fixtures;
using WordGame.Application.Games;
using WordGame.Application.Sessions;
using WordGame.Domain;

namespace WordGame.Api.Tests;

public sealed class CoreGameplayFlowTests
{
    [Theory]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(4, false)]
    [InlineData(2, true)]
    public async Task RealJsonAndStoreSupportCompleteMatchesWithoutRuleDrift(int count, bool online)
    {
        var clock = new ControlledGameplayClock();
        using var root = new WordGameApiFactory();
        using var factory = root.WithWebHostBuilder(builder =>
        {
            // The production engine/store/providers are retained; manual time makes resolution deterministic.
            builder.ConfigureServices(services => services.AddSingleton<TimeProvider>(clock));
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Gameplay:TimeoutSweepInterval"] = "01:00:00",
                    ["Sessions:CleanupInterval"] = "01:00:00"
                }));
        });
        var engine = factory.Services.GetRequiredService<GameEngine>();
        var store = factory.Services.GetRequiredService<IGameSessionStore>();
        var players = Enumerable.Range(0, count).Select(index => new Player(Guid.NewGuid(), $"Player {index}")).ToArray();
        var settings = online ? MatchSettings.Online() : MatchSettings.Party(count * 2);
        var created = Accepted(await engine.CreateAsync(players, settings));
        var id = created.Id;
        foreach (var player in players)
        {
            Accepted(await engine.ConnectAsync(id, player.Id, Guid.NewGuid()));
        }

        Accepted(await engine.InitializeAsync(id));
        Accepted(await engine.StartAsync(id));
        var totals = players.ToDictionary(player => player.Id, _ => 0);
        var history = new List<RoundResult>();
        async Task<PlayerActionContext> Context() => new(Guid.NewGuid(), (await store.GetAsync(id))!.Version);
        for (var roundNumber = 1; roundNumber <= settings.RoundCount; roundNumber++)
        {
            var state = (await store.GetAsync(id))!;
            var starter = players[(roundNumber - 1) % count].Id;
            Assert.Equal(starter, state.Game.CurrentRound!.StartingPlayerId);
            Assert.Null(state.SelectedCategory);
            Assert.Null(state.Game.CurrentRound.HighestBid);
            Assert.Null(state.Game.CurrentRound.Challenge);
            Assert.Equal(totals.Values.Sum(), state.Game.Scores.Values.Sum(score => score.Points));
            var sample = Samples[(roundNumber - 1) % Samples.Length];
            Accepted(await engine.SelectCategoryAsync(id, starter, sample.CategoryId, await Context()));
            var openingContext = await Context();
            var opening = Accepted(await engine.PlaceBidAsync(id, starter, 1, openingContext));
            var bidder = opening.Game.CurrentRound!.Turn.PlayerId;
            var raised = Accepted(await engine.PlaceBidAsync(id, bidder, 3, await Context()));
            var caller = raised.Game.CurrentRound!.Turn.PlayerId;
            Accepted(await engine.CallBluffAsync(id, caller, await Context()));
            Accepted(await engine.SubmitWordAsync(id, bidder, sample.First, await Context()));
            var duplicate = await engine.SubmitWordAsync(id, bidder, sample.Duplicate, await Context());
            Accepted(duplicate);
            Assert.Equal(WordSubmissionStatus.Duplicate, duplicate.SubmissionStatus);
            var rejected = await engine.SubmitWordAsync(id, bidder, "not a valid answer", await Context());
            Accepted(rejected);
            Assert.Equal(WordSubmissionStatus.Rejected, rejected.SubmissionStatus);
            GameEngineResult completion;
            var success = roundNumber % 2 == 1;
            if (success)
            {
                Accepted(await engine.SubmitWordAsync(id, bidder, sample.Second, await Context()));
                completion = await engine.SubmitWordAsync(id, bidder, sample.Third, await Context());
                totals[bidder] += 3 * sample.Points + 50;
            }
            else
            {
                clock.Advance(TimeSpan.FromSeconds(30));
                completion = await engine.ResolveChallengeAsync(id);
                totals[bidder] += sample.Points;
                totals[caller] += 50;
            }
            var completed = Accepted(completion);
            var result = Assert.IsType<RoundResult>(completion.RoundResult);
            history.Add(result);
            Assert.Equal(success ? bidder : caller, result.WinnerId);
            Assert.Equal(success ? 3 : 1, result.AcceptedWordCount);
            Assert.Equal(success ? 5 : 3, result.Submissions.Count);
            foreach (var score in result.Scores)
            {
                Assert.Equal(totals[score.PlayerId], score.TotalPoints);
            }

            foreach (var previous in history)
            {
                Assert.Same(previous, (await engine.GetRoundResultAsync(id, previous.RoundId)).RoundResult);
            }

            Assert.Equal(GameEngineErrorCode.StaleState, (await engine.PlaceBidAsync(id, starter, 4, openingContext)).Error!.Code);
            Assert.Equal(GameEngineErrorCode.InvalidState, (await engine.ResolveChallengeAsync(id)).Error!.Code);
            Assert.Equal(completed.Version, (await store.GetAsync(id))!.Version);
            if (roundNumber < settings.RoundCount)
            {
                Assert.Null(completion.MatchResult);
                Accepted(await engine.NextRoundAsync(id, result.RoundId, completed.Version));
            }
            else
            {
                Assert.Equal(SessionStatus.Completed, completed.Status);
                Assert.Equal(clock.GetUtcNow(), completed.EndedAt);
                Assert.Equal(settings.Format, completion.MatchResult!.Format);
            }
        }
        var final = (await engine.GetStateAsync(id)).MatchResult!;
        var winningScore = totals.Values.Max();
        Assert.Equal(players.Where(player => totals[player.Id] == winningScore).Select(player => player.Id), final.WinnerIds);
        foreach (var standing in final.Standings)
        {
            Assert.Equal(totals[standing.PlayerId], standing.Score);
            Assert.Equal(1 + totals.Values.Count(score => score > standing.Score), standing.Rank);
        }
        foreach (var player in players)
        {
            using var client = AuthenticatedPlayerClient.Create(factory, player.Id);
            using var response = await client.GetAsync($"/api/v1/games/{id}/state");
            response.EnsureSuccessStatusCode();
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("completed", json.RootElement.GetProperty("status").GetString());
            Assert.Equal(winningScore, json.RootElement.GetProperty("result").GetProperty("standings")[0].GetProperty("score").GetInt32());
        }
    }

    private static SessionSnapshot Accepted(GameEngineResult result)
    {
        Assert.True(result.Succeeded, result.Error?.Message);
        return result.State!;
    }

    private sealed record CategorySample(string CategoryId, int Points, string First, string Duplicate, string Second, string Third);
    private static readonly CategorySample[] Samples =
    [
        new("european-capitals", 1, "Stockholm", "STOCKHOLM", "Paris", "Berlin"),
        new("chemical-elements", 2, "Hydrogen", "H", "Oxygen", "Carbon"),
        new("greek-letters", 3, "Alpha", "α", "Beta", "Gamma")
    ];
}
