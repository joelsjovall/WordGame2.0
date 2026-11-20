using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using WordGame.Api.Contracts.V1;
using WordGame.Api.Endpoints;
using WordGame.Application.Matchmaking;
using WordGame.Application.Sessions;

namespace WordGame.Api.Synchronization;

internal static class MatchmakingEventStream
{
    internal static async IAsyncEnumerable<SseItem<MatchmakingResponse>> ReadAsync(GuestSession session,
        IMatchmakingQueueStore queue, MatchmakingService matchmaking, TimeProvider clock,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, session.Revoked);
        var remaining = session.ExpiresAt - clock.GetUtcNow();
        if (remaining <= TimeSpan.Zero) { yield break; }
        lifetime.CancelAfter(remaining);
        await using var changes = queue.WatchAsync(session.PlayerId, lifetime.Token).GetAsyncEnumerator(lifetime.Token);
        var next = changes.MoveNextAsync().AsTask();
        try
        {
            while (!lifetime.IsCancellationRequested)
            {
                var heartbeat = false;
                bool available;
                try { available = await next.WaitAsync(TimeSpan.FromSeconds(15), lifetime.Token); }
                catch (TimeoutException) { available = true; heartbeat = true; }
                catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { break; }
                if (!available) { break; }
                MatchmakingSearch? search;
                try { search = await matchmaking.GetAsync(session.PlayerId, session.Id, cancellationToken: lifetime.Token); }
                catch (UnauthorizedAccessException) { break; }
                catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { break; }
                yield return new SseItem<MatchmakingResponse>(MatchmakingEndpoints.Map(search), "matchmaking")
                { ReconnectionInterval = TimeSpan.FromSeconds(2) };
                if (!heartbeat) { next = changes.MoveNextAsync().AsTask(); }
            }
        }
        finally
        {
            await lifetime.CancelAsync();
            try { await next; } catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        }
    }
}
