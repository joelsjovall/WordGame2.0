using WordGame.Application.Sessions;
using WordGame.Domain;

namespace WordGame.Application.Lobbies;

public sealed class LobbyService(ILobbySessionStore sessions)
{
    public Task<Lobby?> JoinAsync(Guid playerId, string lobbyCode, string displayName, Guid? expectedLobbyId = null,
        CancellationToken cancellationToken = default)
    {
        if (expectedLobbyId == Guid.Empty)
        {
            throw new ArgumentException("An invitation requires a nonempty lobby ID.", nameof(expectedLobbyId));
        }

        return sessions.JoinAsync(new LobbyCode(lobbyCode), new Player(playerId, displayName), expectedLobbyId, cancellationToken);
    }

    public Task<LobbyCreation> CreateAsync(Guid hostPlayerId, Guid requestId, string displayName, CancellationToken cancellationToken = default)
    {
        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("A nonempty request ID is required.", nameof(requestId));
        }

        var host = new Player(hostPlayerId, displayName);
        return sessions.CreateAsync(host, requestId, cancellationToken);
    }
}
