using WordGame.Application.Sessions;
using WordGame.Domain;

namespace WordGame.Application.Matchmaking;

public sealed class MatchmakingService(IMatchmakingQueueStore queue, IGuestSessionStore guests,
    IGameSessionStore games, IMatchmakingPolicy policy, MatchmakingRules rules, TimeProvider clock, OnlineMatchLifecycle online)
{
    public Task<MatchmakingSearch> JoinAsync(Guid playerId, Guid sessionId, string displayName, CancellationToken cancellationToken = default)
    {
        var player = new Player(playerId, displayName);
        player.ValidateDisplayName();
        return queue.ExecuteAsync(async state =>
        {
            RequireSession(playerId, sessionId);
            var assigned = await AssignedPlayers(cancellationToken);
            RequireSession(playerId, sessionId);
            await RefreshAsync(state, assigned);
            var existing = Current(state, playerId);
            if (existing?.Status == MatchmakingStatus.Ready) { return existing; }
            if (existing?.Status is MatchmakingStatus.Searching or MatchmakingStatus.Matched)
            {
                if (existing.Player.DisplayName != player.DisplayName)
                {
                    throw new MatchmakingConflictException("Cancel the existing search before changing your display name.");
                }
                return existing;
            }
            if (assigned.Contains(playerId))
            {
                throw new MatchmakingConflictException("Leave or finish your open match before searching.");
            }
            var now = clock.GetUtcNow();
            var search = new MatchmakingSearch(Guid.NewGuid(), player, sessionId, MatchmakingStatus.Searching,
                now, now.Add(rules.SearchLifetime), now);
            state.Searches.Add(search.Id, search);
            state.LatestByPlayer[playerId] = search.Id;
            state.Waiting.AddLast(search.Id);
            await PairAsync(state);
            return state.Searches[search.Id];
        }, cancellationToken);
    }

    public Task<MatchmakingSearch?> GetAsync(Guid playerId, Guid sessionId, Guid? searchId = null, CancellationToken cancellationToken = default) =>
        queue.ExecuteAsync(async state =>
        {
            RequireSession(playerId, sessionId);
            await RefreshAsync(state, await AssignedPlayers(cancellationToken));
            RequireSession(playerId, sessionId);
            return searchId is { } id ? state.Searches.GetValueOrDefault(id) is { } search && search.Player.Id == playerId ? search : null
                : Current(state, playerId);
        }, cancellationToken);

    public Task<bool> CancelAsync(Guid playerId, Guid sessionId, Guid searchId, CancellationToken cancellationToken = default) =>
        queue.ExecuteAsync(async state =>
        {
            RequireSession(playerId, sessionId);
            await RefreshAsync(state, await AssignedPlayers(cancellationToken));
            RequireSession(playerId, sessionId);
            if (!state.Searches.TryGetValue(searchId, out var search) || search.Player.Id != playerId)
            {
                return false;
            }
            if (search.Status is MatchmakingStatus.Matched or MatchmakingStatus.Ready)
            {
                throw new MatchmakingConflictException("An opponent has already been selected. The reservation will expire if not accepted.");
            }
            if (search.Status == MatchmakingStatus.Searching)
            {
                state.Searches[searchId] = search with { Status = MatchmakingStatus.Cancelled, UpdatedAt = clock.GetUtcNow() };
                state.Waiting.Remove(searchId);
            }
            return true;
        }, cancellationToken);

    public Task<int> CleanupAsync(CancellationToken cancellationToken = default) => queue.ExecuteAsync(async state =>
    {
        var before = state.Searches.Count;
        await RefreshAsync(state, await AssignedPlayers(cancellationToken));
        return before - state.Searches.Count;
    }, cancellationToken);

    private void RequireSession(Guid playerId, Guid sessionId)
    {
        if (guests.Get(sessionId)?.PlayerId != playerId)
        {
            throw new UnauthorizedAccessException("The guest session is no longer valid.");
        }
    }

    private async Task<HashSet<Guid>> AssignedPlayers(CancellationToken cancellationToken) =>
        (await games.GetOpenAsync(cancellationToken)).SelectMany(game => game.Players)
        .Where(player => !player.HasLeft).Select(player => player.PlayerId).ToHashSet();

    private static MatchmakingSearch? Current(MatchmakingQueueState state, Guid playerId) =>
        state.LatestByPlayer.TryGetValue(playerId, out var id) ? state.Searches.GetValueOrDefault(id) : null;

    private async Task RefreshAsync(MatchmakingQueueState state, HashSet<Guid> assigned)
    {
        await online.RefreshAsync(state);
        var now = clock.GetUtcNow();
        var invalid = state.Searches.Values.Where(search => search.Status == MatchmakingStatus.Searching)
            .Where(search => now >= search.ExpiresAt || guests.Get(search.CredentialSessionId)?.PlayerId != search.Player.Id || assigned.Contains(search.Player.Id)).ToArray();
        var invalidIds = invalid.Select(search => search.Id).ToHashSet();
        foreach (var search in state.Searches.Values.ToArray())
        {
            if (search.Status == MatchmakingStatus.Searching && invalidIds.Contains(search.Id))
            {
                state.Searches[search.Id] = search with { Status = MatchmakingStatus.Expired, UpdatedAt = now };
                state.Waiting.Remove(search.Id);
            }
            else if (search.Status is MatchmakingStatus.Cancelled or MatchmakingStatus.Expired or MatchmakingStatus.Failed && now - search.UpdatedAt >= rules.TerminalRetention)
            {
                state.Searches.Remove(search.Id);
                if (state.LatestByPlayer.GetValueOrDefault(search.Player.Id) == search.Id)
                {
                    state.LatestByPlayer.Remove(search.Player.Id);
                }
            }
        }
    }

    private async Task PairAsync(MatchmakingQueueState state)
    {
        while (state.Waiting.Count > 0)
        {
            var waiting = state.Waiting.Select(id => state.Searches[id]).ToArray();
            var group = policy.SelectGroup(waiting);
            if (group.Count == 0)
            {
                return;
            }
            if (group.Count < 2 || group.Select(search => search.Player.Id).Distinct().Count() != group.Count ||
                group.Any(search => !state.Waiting.Contains(search.Id)))
            {
                throw new InvalidOperationException("The matchmaking policy selected an invalid group.");
            }
            var matchId = Guid.NewGuid();
            var gameId = await online.CreateAsync(group);
            var now = clock.GetUtcNow();
            foreach (var search in group)
            {
                state.Searches[search.Id] = search with
                {
                    Status = gameId.HasValue ? MatchmakingStatus.Matched : MatchmakingStatus.Failed,
                    MatchId = matchId,
                    GameId = gameId,
                    ExpiresAt = now.Add(rules.AcceptanceTimeout),
                    UpdatedAt = now
                };
                state.Waiting.Remove(search.Id);
            }
        }
    }

    public Task<MatchmakingSearch?> RespondAsync(Guid playerId, Guid sessionId, Guid searchId, bool accept, CancellationToken cancellationToken = default) =>
        queue.ExecuteAsync<MatchmakingSearch?>(async state =>
        {
            RequireSession(playerId, sessionId);
            await RefreshAsync(state, await AssignedPlayers(cancellationToken));
            RequireSession(playerId, sessionId);
            if (!state.Searches.TryGetValue(searchId, out var search) || search.Player.Id != playerId) { return null; }
            if (accept) { return await online.AcceptAsync(state, search); }
            if (search.Status == MatchmakingStatus.Cancelled) { return search; }
            if (search.Status != MatchmakingStatus.Matched) { throw new MatchmakingConflictException("Only an unaccepted match can be declined."); }
            await online.EndAsync(state, search, MatchmakingStatus.Cancelled, true);
            return state.Searches[search.Id];
        }, cancellationToken);
}
