using System.Globalization;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using WordGame.Api.Contracts.V1;
using WordGame.Api.Mapping;
using WordGame.Application.Games;
using WordGame.Application.Sessions;
using WordGame.Application.Synchronization;

namespace WordGame.Api.Synchronization;

internal static class GameEventStream
{
    internal static async IAsyncEnumerable<SseItem<GameEventResponse>> ReadAsync(SessionSnapshot initial, Guid playerId,
        DateTimeOffset expires, GameEventService events, GameEngine engine, TimeProvider clock,
        CancellationToken credentialRevoked, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, credentialRevoked);
        var remaining = expires - clock.GetUtcNow();
        if (remaining <= TimeSpan.Zero)
        {
            yield break;
        }

        lifetime.CancelAfter(remaining);
        var connectionId = Guid.NewGuid();
        var connected = false;
        if (initial.Status is SessionStatus.Waiting or SessionStatus.Starting or SessionStatus.Active && !initial.Players.Single(player => player.PlayerId == playerId).HasLeft)
        {
            var result = await engine.ConnectAsync(initial.Id, playerId, connectionId, lifetime.Token);
            connected = result.Succeeded && result.State!.Players.Single(player => player.PlayerId == playerId).ConnectionCount > 0;
        }
        try
        {
            await using var changes = events.WatchAsync(initial.Id, playerId, lifetime.Token).GetAsyncEnumerator(lifetime.Token);
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15), clock);
            var heartbeat = timer.WaitForNextTickAsync(lifetime.Token).AsTask();
            var next = changes.MoveNextAsync().AsTask();
            var last = new SessionChange(initial.Id, initial.Version, initial.Status, SessionChangeKind.Updated);
            try
            {
                while (!lifetime.IsCancellationRequested)
                {
                    var ready = await Task.WhenAny(next, heartbeat);
                    if (ready == next)
                    {
                        bool available;
                        try
                        {
                            available = await next;
                        }
                        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
                        {
                            break;
                        }
                        if (!available)
                        {
                            break;
                        }

                        last = changes.Current;
                        yield return Item(last, "game");
                        if (last.Kind == SessionChangeKind.Removed)
                        {
                            break;
                        }

                        next = changes.MoveNextAsync().AsTask();
                    }
                    else
                    {
                        try
                        {
                            if (!await heartbeat)
                            {
                                break;
                            }
                        }
                        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
                        {
                            break;
                        }
                        yield return Item(last, "heartbeat");
                        heartbeat = timer.WaitForNextTickAsync(lifetime.Token).AsTask();
                    }
                }
            }
            finally
            {
                await lifetime.CancelAsync();
                // Drain an outstanding read before disposing its async iterator.
                try
                {
                    await next;
                }
                catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
                try
                {
                    await heartbeat;
                }
                catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
            }
        }
        finally
        {
            if (connected)
            {
                // Request cancellation must not prevent releasing this connection's presence.
                _ = await engine.DisconnectAsync(initial.Id, playerId, connectionId, CancellationToken.None);
            }
        }
    }

    private static SseItem<GameEventResponse> Item(SessionChange change, string eventType)
    {
        var version = change.Version.ToString(CultureInfo.InvariantCulture);
        return new SseItem<GameEventResponse>(new GameEventResponse(change.SessionId, version, GameStateMapper.MapStatus(change.Status),
            change.Kind == SessionChangeKind.Removed ? ApiEventKind.Removed : ApiEventKind.Updated), eventType)
        { EventId = version, ReconnectionInterval = TimeSpan.FromSeconds(2) };
    }
}
