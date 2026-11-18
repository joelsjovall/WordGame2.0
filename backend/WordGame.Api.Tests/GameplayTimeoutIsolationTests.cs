using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WordGame.Api.Tests.Fixtures;
using WordGame.Application.Categories;
using WordGame.Application.Games;
using WordGame.Application.Sessions;
using WordGame.Domain;
using WordGame.Infrastructure.Categories;

namespace WordGame.Api.Tests;

public sealed class GameplayTimeoutIsolationTests
{
    [Fact]
    public async Task SlowCategoryProviderCannotBlockAnotherGamesChallengeExpiry()
    {
        var clock = new ControlledGameplayClock();
        using var factory = new WordGameApiFactory().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Gameplay:CategorySelectionTimeout"] = "00:00:01",
                    ["Gameplay:ChallengeDuration"] = "00:00:02",
                    ["Gameplay:TimeoutSweepInterval"] = "00:00:00.010"
                }));
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<TimeProvider>(clock);
                services.AddSingleton<ICategoryProvider>(provider => new PausedProvider(new JsonCategoryProvider(
                    Path.Combine(provider.GetRequiredService<IWebHostEnvironment>().ContentRootPath, "Data", "Categories"))));
            });
        });
        var engine = factory.Services.GetRequiredService<GameEngine>();
        var store = factory.Services.GetRequiredService<IGameSessionStore>();
        var provider = (PausedProvider)factory.Services.GetRequiredService<ICategoryProvider>();
        var players = new[] { new Player(Guid.NewGuid(), "Alice"), new Player(Guid.NewGuid(), "Bob") };
        var first = (await engine.CreateAsync(players)).State!.Id;
        Assert.True((await engine.InitializeAsync(first)).Succeeded);
        Assert.True((await engine.StartAsync(first)).Succeeded);
        Assert.True((await engine.SelectCategoryAsync(first, players[0].Id, "european-capitals",
            new(Guid.NewGuid(), (await store.GetAsync(first))!.Version))).Succeeded);
        Assert.True((await engine.PlaceBidAsync(first, players[0].Id, 2, new(Guid.NewGuid(), (await store.GetAsync(first))!.Version))).Succeeded);
        Assert.True((await engine.CallBluffAsync(first, players[1].Id, new(Guid.NewGuid(), (await store.GetAsync(first))!.Version))).Succeeded);
        var second = (await engine.CreateAsync(players)).State!.Id;
        Assert.True((await engine.InitializeAsync(second)).Succeeded);
        Assert.True((await engine.StartAsync(second)).Succeeded);
        provider.BlockReads = true;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            clock.Advance(TimeSpan.FromSeconds(1));
            await provider.Entered.Task.WaitAsync(timeout.Token);
            clock.Advance(TimeSpan.FromSeconds(1));
            await foreach (var change in store.WatchAsync(first, timeout.Token))
            {
                var result = await engine.GetStateAsync(change.SessionId, timeout.Token);
                if (result.ChallengeResult is null)
                {
                    continue;
                }

                Assert.Equal(ChallengeOutcome.Failed, result.ChallengeResult.Outcome);
                break;
            }
        }
        finally
        {
            provider.Release.TrySetResult();
        }
    }

    private sealed class PausedProvider(ICategoryProvider inner) : ICategoryProvider
    {
        internal volatile bool BlockReads;
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<IReadOnlyList<Category>> GetCategoriesAsync(CategoryDifficulty? difficulty = null, CancellationToken cancellationToken = default) =>
            inner.GetCategoriesAsync(difficulty, cancellationToken);
        public async Task<CategoryDefinition?> GetByIdAsync(string categoryId, CancellationToken cancellationToken = default)
        {
            if (BlockReads)
            {
                Entered.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }
            return await inner.GetByIdAsync(categoryId, cancellationToken);
        }
    }
}
