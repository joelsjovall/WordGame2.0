using System.Runtime.CompilerServices;
using System.Threading.Channels;
using WordGame.Application.Matchmaking;

namespace WordGame.Infrastructure.Sessions;

public sealed class InMemoryMatchmakingQueueStore : IMatchmakingQueueStore, IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly MatchmakingQueueState state = new();
    private readonly Dictionary<Guid, HashSet<Channel<MatchmakingSearch?>>> watchers = [];

    public async Task<T> ExecuteAsync<T>(Func<MatchmakingQueueState, Task<T>> transition, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        var before = state.LatestByPlayer.ToDictionary(pair => pair.Key, pair => state.Searches.GetValueOrDefault(pair.Value));
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await transition(state);
        }
        finally
        {
            foreach (var pair in watchers)
            {
                var current = Current(pair.Key);
                if (current != before.GetValueOrDefault(pair.Key))
                {
                    foreach (var channel in pair.Value) { channel.Writer.TryWrite(current); }
                }
            }
            gate.Release();
        }
    }

    public async IAsyncEnumerable<MatchmakingSearch?> WatchAsync(Guid playerId, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var channel = Channel.CreateBounded<MatchmakingSearch?>(new BoundedChannelOptions(1)
        { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true, AllowSynchronousContinuations = false });
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (!watchers.TryGetValue(playerId, out var channels)) { watchers[playerId] = channels = []; }
            channels.Add(channel);
            channel.Writer.TryWrite(Current(playerId));
        }
        finally { gate.Release(); }
        try
        {
            await foreach (var search in channel.Reader.ReadAllAsync(cancellationToken)) { yield return search; }
        }
        finally
        {
            await gate.WaitAsync(CancellationToken.None);
            try
            {
                if (watchers.TryGetValue(playerId, out var channels) && channels.Remove(channel) && channels.Count == 0) { watchers.Remove(playerId); }
            }
            finally { gate.Release(); }
        }
    }
    private MatchmakingSearch? Current(Guid playerId) => state.LatestByPlayer.TryGetValue(playerId, out var id) ? state.Searches.GetValueOrDefault(id) : null;
    public void Dispose() => gate.Dispose();
}
