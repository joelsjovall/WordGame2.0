using WordGame.Api.Errors;
using WordGame.Application.Matchmaking;

namespace WordGame.Api.Sessions;

public sealed class MatchmakingWorker(MatchmakingService matchmaking, OnlineRematchService rematches, TimeProvider clock, IConfiguration configuration,
    ILogger<MatchmakingWorker> logger, IHostEnvironment environment) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = configuration.GetValue("Matchmaking:CleanupInterval", TimeSpan.FromSeconds(1));
        if (interval <= TimeSpan.Zero) { throw new InvalidOperationException("Matchmaking cleanup interval must be positive."); }
        using var timer = new PeriodicTimer(interval, clock);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try { await matchmaking.CleanupAsync(stoppingToken); await rematches.CleanupAsync(stoppingToken); }
                catch (Exception exception) when (exception is not OperationCanceledException)
                { SafeErrorLogging.Log(logger, environment, exception, "Matchmaking cleanup"); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
