using WordGame.Application.Games;
using WordGame.Application.Sessions;
using WordGame.Domain;

namespace WordGame.Application.Lobbies;

public sealed class PartyLifecycleService(ILobbySessionStore lobbies, GameEngine engine, LobbyRules rules, TimeProvider clock)
{
    public Task<IReadOnlyList<Lobby>> GetPlayingAsync(CancellationToken cancellationToken = default) => lobbies.GetPlayingAsync(cancellationToken);

    public async Task RefreshAsync(Lobby lobby, CancellationToken cancellationToken = default)
    {
        if (lobby.Status != LobbyStatus.Started || lobby.GameId is not { } id)
        {
            return;
        }

        var state = (await engine.GetStateAsync(id, cancellationToken)).State;
        if (state is null || state.Status is SessionStatus.Completed or SessionStatus.Abandoned)
        {
            await lobbies.UpdateAsync(lobby.Id, current => current.FinishMatch(id, state?.Game.Result, clock.GetUtcNow(), rules.Lifetime), cancellationToken);
        }
    }
}
