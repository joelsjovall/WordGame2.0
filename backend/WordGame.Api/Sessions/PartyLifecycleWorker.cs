using WordGame.Api.Errors;
using WordGame.Application.Lobbies;

namespace WordGame.Api.Sessions;

public sealed class PartyLifecycleWorker(PartyLifecycleService lifecycle, TimeProvider clock, ILogger<PartyLifecycleWorker> logger,
    IHostEnvironment environment) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1), clock);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                foreach (var lobby in await lifecycle.GetPlayingAsync(stoppingToken))
                {
                    try { await lifecycle.RefreshAsync(lobby, stoppingToken); }
                    catch (Exception exception) when (exception is not OperationCanceledException)
                    { SafeErrorLogging.Log(logger, environment, exception, $"Party lifecycle update for lobby {lobby.Id}"); }
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
