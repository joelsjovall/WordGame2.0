using WordGame.Application.Lobbies;

namespace WordGame.Api.Sessions;

public sealed class PartyLifecycleWorker(PartyLifecycleService lifecycle, TimeProvider clock, ILogger<PartyLifecycleWorker> logger) : BackgroundService
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
                    { logger.LogError(exception, "Party lifecycle update failed for {LobbyId}; a later sweep will retry.", lobby.Id); }
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
