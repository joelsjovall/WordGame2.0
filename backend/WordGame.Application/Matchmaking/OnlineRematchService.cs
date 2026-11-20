using WordGame.Application.Games;
using WordGame.Application.Sessions;
using WordGame.Domain;

namespace WordGame.Application.Matchmaking;

public enum RematchStatus { Idle, Pending, Ready, Declined, Expired, Unavailable }
public enum RematchAction { Request, Accept, Decline }
public sealed record OnlineRematch(Guid SourceGameId, RematchStatus Status, IReadOnlyList<Guid> AcceptedPlayerIds,
    DateTimeOffset? ExpiresAt = null, Guid? GameId = null);

// Queue transitions and rematch acceptance share one gate, preventing races with a new search.
public sealed class OnlineRematchService(IMatchmakingQueueStore queue, IGameSessionStore games,
    IGuestSessionStore guests, GameEngine engine, TimeProvider clock)
{
    private readonly Dictionary<Guid, OnlineRematch> rematches = [];
    private readonly Dictionary<Guid, Dictionary<Guid, Guid>> credentials = [];
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    public Task<OnlineRematch> GetAsync(Guid gameId, Guid playerId, Guid sessionId, CancellationToken cancellationToken = default) =>
        queue.ExecuteAsync(async state =>
        {
            await Source(gameId, playerId, sessionId);
            await Refresh(state);
            return rematches.GetValueOrDefault(gameId) ?? new(gameId, RematchStatus.Idle, []);
        }, cancellationToken);

    public Task<OnlineRematch> RespondAsync(Guid gameId, Guid playerId, Guid sessionId, RematchAction action,
        CancellationToken cancellationToken = default) => queue.ExecuteAsync(async state =>
    {
        var source = await Source(gameId, playerId, sessionId);
        await Refresh(state);
        var current = rematches.GetValueOrDefault(gameId) ?? new(gameId, RematchStatus.Idle, []);
        if (current.Status is not (RematchStatus.Idle or RematchStatus.Pending)) { return current; }
        if (action == RematchAction.Accept && current.Status != RematchStatus.Pending)
        { throw new MatchmakingConflictException("There is no rematch request to accept."); }
        if (action == RematchAction.Decline)
        {
            return await Save(current with { Status = RematchStatus.Declined });
        }
        if (current.AcceptedPlayerIds.Contains(playerId)) { return current; }
        if (await Unavailable(state, source))
        { return await Save(current with { Status = RematchStatus.Unavailable }); }
        var accepted = current.AcceptedPlayerIds.Append(playerId).Distinct().ToArray();
        if (!credentials.TryGetValue(gameId, out var sessions)) { credentials[gameId] = sessions = []; }
        sessions[playerId] = sessionId;
        current = current with { Status = RematchStatus.Pending, AcceptedPlayerIds = accepted, ExpiresAt = current.ExpiresAt ?? clock.GetUtcNow().Add(Timeout) };
        if (accepted.Length == 2)
        {
            Guid? created = null;
            try
            {
                var result = await engine.CreateExclusiveAsync(source.Game.Players, source.Game.Settings);
                if (result.Succeeded)
                {
                    created = result.State!.Id;
                    if ((await engine.InitializeAsync(created.Value)).Succeeded && (await engine.StartAsync(created.Value)).Succeeded)
                    { return await Save(current with { Status = RematchStatus.Ready, GameId = created }); }
                }
            }
            catch (Exception)
            {
                if (created is { } failed) { await engine.CancelAsync(failed); }
                throw;
            }
            if (created is { } id) { await engine.CancelAsync(id); }
            return await Save(current with { Status = RematchStatus.Unavailable });
        }
        return await Save(current);
    }, cancellationToken);

    public Task<int> CleanupAsync(CancellationToken cancellationToken = default) => queue.ExecuteAsync(async state =>
    {
        await Refresh(state); return rematches.Count;
    }, cancellationToken);

    private async Task<SessionSnapshot> Source(Guid id, Guid actor, Guid credential)
    {
        if (guests.Get(credential)?.PlayerId != actor) { throw new UnauthorizedAccessException(); }
        var source = await games.GetAsync(id);
        if (source is null || !source.Game.Players.Any(player => player.Id == actor)) { throw new KeyNotFoundException(); }
        if (source.Status != SessionStatus.Completed || source.Game.Settings.Format != MatchFormat.Online)
        { throw new MatchmakingConflictException("Rematches require a completed Online match."); }
        return source;
    }

    private async Task<bool> Unavailable(MatchmakingQueueState state, SessionSnapshot source)
    {
        var ids = source.Game.Players.Select(player => player.Id).ToHashSet();
        return state.Searches.Values.Any(search => ids.Contains(search.Player.Id) && search.Status is MatchmakingStatus.Searching or MatchmakingStatus.Matched) ||
            (await games.GetOpenAsync()).Any(game => game.Players.Any(player => ids.Contains(player.PlayerId) && !player.HasLeft));
    }

    private async Task Refresh(MatchmakingQueueState state)
    {
        foreach (var current in rematches.Values.ToArray())
        {
            var source = await games.GetAsync(current.SourceGameId);
            if (source is null) { rematches.Remove(current.SourceGameId); credentials.Remove(current.SourceGameId); continue; }
            if (current.Status != RematchStatus.Pending) { continue; }
            if (clock.GetUtcNow() >= current.ExpiresAt || credentials[current.SourceGameId].Any(pair => guests.Get(pair.Value)?.PlayerId != pair.Key))
            { await Save(current with { Status = RematchStatus.Expired }); }
            else if (await Unavailable(state, source)) { await Save(current with { Status = RematchStatus.Unavailable }); }
        }
    }

    private async Task<OnlineRematch> Save(OnlineRematch value)
    {
        rematches[value.SourceGameId] = value;
        // Reuse the completed game's SSE invalidation and revision recovery without a second stream.
        if (await games.GetAsync(value.SourceGameId) is not null)
        {
            try { await games.UpdateAsync(value.SourceGameId, _ => { }); }
            catch (KeyNotFoundException) { /* Retention cleanup removed the source concurrently. */ }
        }
        return value;
    }
}
