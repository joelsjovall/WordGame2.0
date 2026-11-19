using WordGame.Application.Sessions;
using WordGame.Domain;

namespace WordGame.Application.Lobbies;

public sealed class LobbyPlayerService(ILobbySessionStore lobbies, TimeProvider clock)
{
    public Task<Lobby?> SetReadyAsync(Guid id, Guid actor, long expectedVersion, bool ready, CancellationToken cancellationToken = default) =>
        lobbies.UpdateAsync(id, lobby =>
        {
            lobby.Roster.RequireMember(actor);
            if (lobby.Version != expectedVersion)
            {
                throw new LobbyActionException("The lobby changed. Refresh and try again.");
            }

            return lobby.SetReady(actor, ready);
        }, cancellationToken);
    public Task<Lobby?> LeaveAsync(Guid id, Guid actor, CancellationToken cancellationToken = default) =>
        lobbies.UpdateAsync(id, lobby => lobby.Leave(actor), cancellationToken);
    public Task<Lobby?> RemoveAsync(Guid id, Guid actor, Guid target, CancellationToken cancellationToken = default) =>
        lobbies.UpdateAsync(id, lobby => lobby.RemovePlayer(actor, target), cancellationToken);
    public Task<Lobby?> ConnectAsync(Guid id, Guid actor, Guid connectionId, CancellationToken cancellationToken = default) =>
        lobbies.UpdateAsync(id, lobby => lobby.Connect(actor, connectionId), cancellationToken);
    public Task<Lobby?> DisconnectAsync(Guid id, Guid actor, Guid connectionId, CancellationToken cancellationToken = default) =>
        lobbies.UpdateAsync(id, lobby => lobby.Disconnect(actor, connectionId, clock.GetUtcNow()), cancellationToken);
}
