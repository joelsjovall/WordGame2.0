using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WordGame.Api.Tests.Fixtures;
using WordGame.Application.Categories;
using WordGame.Application.Games;
using WordGame.Application.Sessions;
using WordGame.Domain;

namespace WordGame.Api.Tests;

public sealed class CategorySelectionHostTests
{
    [Fact]
    public async Task HostedTimeoutUsesConfiguredProviderDifficultyAndMultiplier()
    {
        using var factory = new WordGameApiFactory().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Gameplay:CategorySelectionTimeout"] = "00:00:00.050",
                    ["Gameplay:CategorySelectionSweepInterval"] = "00:00:00.010",
                    ["Gameplay:AllowedDifficulties:0"] = "Hard",
                    ["Gameplay:DifficultyMultipliers:Hard"] = "3"
                })));
        var engine = factory.Services.GetRequiredService<GameEngine>();
        var store = factory.Services.GetRequiredService<IGameSessionStore>();
        var selection = factory.Services.GetRequiredService<CategorySelectionService>();
        Assert.All(await selection.GetAvailableAsync(), category => Assert.Equal(CategoryDifficulty.Hard, category.Difficulty));
        var players = new[] { new Player(Guid.NewGuid(), "Alice"), new Player(Guid.NewGuid(), "Bob") };
        var created = await engine.CreateAsync(players);
        Assert.True(created.Succeeded);
        var id = created.State!.Id;
        Assert.True((await engine.InitializeAsync(id)).Succeeded);
        Assert.True((await engine.StartAsync(id)).Succeeded);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await foreach (var change in store.WatchAsync(id, timeout.Token))
        {
            var state = (await store.GetAsync(change.SessionId, timeout.Token))!;
            if (state.Game.CurrentRound?.Status != RoundStatus.Bidding)
            {
                continue;
            }

            Assert.Equal(CategoryDifficulty.Hard, state.Game.CurrentRound.Category!.Difficulty);
            Assert.NotEmpty(state.SelectedCategory!.Definition.Answers);
            Assert.Equal(3, state.SelectedCategory.ScoreMultiplier);
            Assert.Null(state.CategorySelectionDeadline);
            break;
        }
    }
}
