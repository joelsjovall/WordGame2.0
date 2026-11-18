using WordGame.Application.Sessions;

namespace WordGame.Api.Sessions;

public sealed class SessionCleanupWorker : BackgroundService
{
    private readonly IGameSessionStore sessions;
    private readonly TimeProvider clock;
    private readonly ILogger<SessionCleanupWorker> logger;
    private readonly TimeSpan interval;

    public SessionCleanupWorker(
        IGameSessionStore sessions, TimeProvider clock, IConfiguration configuration, ILogger<SessionCleanupWorker> logger)
    {
        this.sessions = sessions;
        this.clock = clock;
        this.logger = logger;
        interval = configuration.GetValue("Sessions:CleanupInterval", TimeSpan.FromMinutes(1));
        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(configuration), "Session cleanup interval must be positive.");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(interval, clock);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    var removed = await sessions.CleanupAsync(stoppingToken);
                    if (removed > 0)
                    {
                        logger.LogInformation("Removed {SessionCount} retained game sessions.", removed);
                    }
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogError(exception, "Game session cleanup failed; a later sweep will retry.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal host shutdown.
        }
    }
}
