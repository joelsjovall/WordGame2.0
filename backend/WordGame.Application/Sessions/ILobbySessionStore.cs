using WordGame.Application.Lobbies;
using WordGame.Domain;

namespace WordGame.Application.Sessions;

public interface ILobbySessionStore
{
    // Same host/request/name replays the original active lobby; a different name conflicts.
    Task<LobbyCreation> CreateAsync(Player host, Guid requestId, CancellationToken cancellationToken = default);
    Task<Lobby?> GetAsync(Guid lobbyId, CancellationToken cancellationToken = default);
    Task<Lobby?> JoinAsync(LobbyCode code, Player player, Guid? expectedLobbyId = null, CancellationToken cancellationToken = default);
    Task<int> CleanupAsync(CancellationToken cancellationToken = default);
}
