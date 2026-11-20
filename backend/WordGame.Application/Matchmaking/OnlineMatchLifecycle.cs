using WordGame.Application.Games;
using WordGame.Application.Sessions;
using WordGame.Domain;

namespace WordGame.Application.Matchmaking;

// Called only inside a serialized queue transition; gameplay always goes through the shared engine.
public sealed class OnlineMatchLifecycle(GameEngine engine, IGameSessionStore games, IGuestSessionStore guests, TimeProvider clock)
{
    public async Task<Guid?> CreateAsync(IReadOnlyList<MatchmakingSearch> group)
    {
        Guid? created = null;
        try
        {
            var result = await engine.CreateExclusiveAsync(group.Select(search => search.Player), MatchSettings.Online());
            if (!result.Succeeded) { return null; }
            created = result.State!.Id;
            if ((await engine.InitializeAsync(created.Value)).Succeeded) { return created; }
        }
        catch (Exception) { /* Return a failed reservation without exposing internal exceptions. */ }
        if (created is { } id) { await engine.CancelAsync(id); }
        return null;
    }

    public async Task RefreshAsync(MatchmakingQueueState state)
    {
        foreach (var group in state.Searches.Values.Where(search => search.Status is MatchmakingStatus.Matched or MatchmakingStatus.Ready)
            .GroupBy(search => search.MatchId).Select(group => group.ToArray()).ToArray())
        {
            var first = group[0];
            var game = first.GameId is { } gameId ? await games.GetAsync(gameId) : null;
            var pending = first.Status == MatchmakingStatus.Matched;
            if (game is null || game.Status is SessionStatus.Completed or SessionStatus.Abandoned ||
                pending && (clock.GetUtcNow() >= first.ExpiresAt || group.Any(search => guests.Get(search.CredentialSessionId)?.PlayerId != search.Player.Id)))
            {
                await EndAsync(state, first, MatchmakingStatus.Expired, pending);
            }
        }
    }

    public async Task<MatchmakingSearch> AcceptAsync(MatchmakingQueueState state, MatchmakingSearch search)
    {
        if (search.Status == MatchmakingStatus.Ready) { return search; }
        if (search.Status != MatchmakingStatus.Matched) { throw new MatchmakingConflictException("This match is no longer available for acceptance."); }
        if (search.HasAccepted) { return search; }
        state.Searches[search.Id] = search with { HasAccepted = true, UpdatedAt = clock.GetUtcNow() };
        var group = state.Searches.Values.Where(candidate => candidate.MatchId == search.MatchId).ToArray();
        if (group.All(candidate => candidate.HasAccepted))
        {
            try
            {
                if (search.GameId is { } gameId && (await engine.StartAsync(gameId)).Succeeded)
                {
                    foreach (var member in group)
                    {
                        state.Searches[member.Id] = member with { Status = MatchmakingStatus.Ready, UpdatedAt = clock.GetUtcNow() };
                    }
                    return state.Searches[search.Id];
                }
            }
            catch (Exception) { /* Roll back this unstarted match below. */ }
            await EndAsync(state, search, MatchmakingStatus.Failed, true);
        }
        return state.Searches[search.Id];
    }

    public async Task EndAsync(MatchmakingQueueState state, MatchmakingSearch search, MatchmakingStatus status, bool cancelGame)
    {
        if (cancelGame && search.GameId is { } id) { await engine.CancelAsync(id); }
        foreach (var member in state.Searches.Values.Where(candidate => candidate.MatchId == search.MatchId).ToArray())
        {
            state.Searches[member.Id] = member with { Status = status, UpdatedAt = clock.GetUtcNow() };
        }
    }
}
