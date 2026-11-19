using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using WordGame.Api.Contracts.V1;
using WordGame.Api.Mapping;
using WordGame.Application.Lobbies;
using WordGame.Application.Sessions;
using WordGame.Domain;

namespace WordGame.Api.Synchronization;

internal static class LobbyEventStream
{
    internal static async IAsyncEnumerable<SseItem<LobbyResponse>> ReadAsync(Lobby initial, Guid actor, DateTimeOffset credentialExpiry,
        ILobbySessionStore store, LobbyPlayerService players, TimeProvider clock, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        // Current-state reads enforce waiting/results expiry; active matches keep their lobby stream.
        var remaining = credentialExpiry - clock.GetUtcNow();
        if (remaining <= TimeSpan.Zero)
        {
            yield break;
        }

        lifetime.CancelAfter(remaining);
        var connectionId = Guid.NewGuid();
        var connected = false;
        try { connected = await players.ConnectAsync(initial.Id, actor, connectionId, lifetime.Token) is not null; }
        catch (LobbyActionException) { /* Membership may have changed after the endpoint authorization check. */ }
        if (!connected)
        {
            yield break;
        }

        try
        {
            await using var changes = store.WatchAsync(initial.Id, lifetime.Token).GetAsyncEnumerator(lifetime.Token);
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
                    if (!available)
                    {
                        break;
                    }

                    // Recheck current membership before every payload, including snapshots queued before a kick.
                    var lobby = await store.GetAsync(initial.Id, lifetime.Token);
                    if (lobby is null || !lobby.Players.Any(player => player.Id == actor))
                    {
                        yield return new SseItem<LobbyResponse>(LobbyMapper.Map(initial, clock.GetUtcNow()), lobby is null ? "expired" : "removed");
                        break;
                    }
                    yield return new SseItem<LobbyResponse>(LobbyMapper.Map(lobby, clock.GetUtcNow()), "lobby")
                    { EventId = lobby.Version.ToString(System.Globalization.CultureInfo.InvariantCulture), ReconnectionInterval = TimeSpan.FromSeconds(2) };
                    if (!heartbeat)
                    {
                        next = changes.MoveNextAsync().AsTask();
                    }
                }
            }
            finally
            {
                await lifetime.CancelAsync();
                try { await next; } catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
            }
        }
        finally
        {
            await players.DisconnectAsync(initial.Id, actor, connectionId, CancellationToken.None);
        }
    }
}
