using WordGame.Application.Categories;
using WordGame.Application.Sessions;
using WordGame.Domain;

namespace WordGame.Api.Sessions;

public sealed class CategorySelectionTimeoutWorker : BackgroundService
{
    private readonly IGameSessionStore sessions;
    private readonly CategorySelectionService categories;
    private readonly TimeProvider clock;
    private readonly ILogger<CategorySelectionTimeoutWorker> logger;
    private readonly TimeSpan interval;

    public CategorySelectionTimeoutWorker(IGameSessionStore sessions, CategorySelectionService categories,
        TimeProvider clock, IConfiguration configuration, ILogger<CategorySelectionTimeoutWorker> logger)
    {
        this.sessions = sessions;
        this.categories = categories;
        this.clock = clock;
        this.logger = logger;
        interval = configuration.GetValue("Gameplay:CategorySelectionSweepInterval", TimeSpan.FromSeconds(1));
        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(configuration), "Category sweep interval must be positive.");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (categories.Rules.Timeout is null)
        {
            return;
        }

        using var timer = new PeriodicTimer(interval, clock);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    foreach (var session in await sessions.GetActiveAsync(stoppingToken))
                    {
                        if (session.Game.CurrentRound?.Status != RoundStatus.CategorySelection ||
                            session.CategorySelectionDeadline is not { } deadline || clock.GetUtcNow() < deadline)
                        {
                            continue;
                        }

                        try
                        {
                            // Stale revisions/finished rounds are harmless races and retry on a later sweep.
                            await categories.ExpireAsync(session.Id, session.Version, stoppingToken);
                        }
                        catch (Exception exception) when (exception is not OperationCanceledException)
                        {
                            logger.LogError(exception, "Category selection timeout failed for {GameId}; a later sweep will retry.", session.Id);
                        }
                    }
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogError(exception, "Category timeout sweep failed; a later sweep will retry.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
