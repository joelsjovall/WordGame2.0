using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using WordGame.Api.Contracts.V1;
using WordGame.Api.Mapping;
using WordGame.Application.Sessions;
using WordGame.Domain;

namespace WordGame.Api.Synchronization;

internal static class LobbyEventStream
{
    internal static async IAsyncEnumerable<SseItem<LobbyResponse>> ReadAsync(Lobby initial, Guid actor, DateTimeOffset credentialExpiry,
        ILobbySessionStore store, TimeProvider clock, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var expiry = credentialExpiry < initial.ExpiresAt ? credentialExpiry : initial.ExpiresAt;
        var remaining = expiry - clock.GetUtcNow();
        if (remaining <= TimeSpan.Zero)
        {
            yield break;
        }

        lifetime.CancelAfter(remaining);
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

                var lobby = heartbeat ? await store.GetAsync(initial.Id, lifetime.Token) : changes.Current;
                if (lobby is null || !lobby.Players.Any(player => player.Id == actor))
                {
                    yield return new SseItem<LobbyResponse>(LobbyMapper.Map(initial), "expired");
                    break;
                }
                yield return new SseItem<LobbyResponse>(LobbyMapper.Map(lobby), "lobby")
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
}
