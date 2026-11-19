using WordGame.Application.Matchmaking;

namespace WordGame.Infrastructure.Sessions;

public sealed class InMemoryMatchmakingQueueStore : IMatchmakingQueueStore, IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly MatchmakingQueueState state = new();

    public async Task<T> ExecuteAsync<T>(Func<MatchmakingQueueState, Task<T>> transition, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await transition(state);
        }
        finally
        {
            gate.Release();
        }
    }
    public void Dispose() => gate.Dispose();
}
