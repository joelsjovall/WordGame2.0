import type { Lobby } from '../../services/api/lobbies.ts'

export function LobbyMembers({
  lobby,
  playerId,
  busy,
  remove,
}: {
  lobby: Lobby
  playerId: string
  busy: boolean
  remove: (target: string) => void
}) {
  const host = playerId === lobby.hostPlayerId && lobby.status === 'waiting'
  return (
    <>
      <ul
        className="lobby-players party-player-list"
        aria-label="Lobby players"
      >
        {lobby.players.map((player) => (
          <li key={player.id}>
            <span className="party-avatar" aria-hidden="true">
              {Array.from(player.displayName.trim())[0]?.toLocaleUpperCase() ??
                '?'}
            </span>
            <div className="party-player-info">
              <span className="party-player-name">
                {player.displayName}
                {player.id === lobby.hostPlayerId && (
                  <span className="party-host-badge"> (host)</span>
                )}
              </span>
              <span
                className={`party-player-status ${player.isReady ? 'is-ready' : ''}`}
              >
                {' '}
                — {player.isConnected ? 'Connected' : 'Reconnecting'};{' '}
                {player.isReady ? 'Ready' : 'Not ready'}
              </span>
            </div>
            {host && player.id !== playerId && (
              <button
                type="button"
                disabled={busy}
                onClick={() => remove(player.id)}
                aria-label={`Remove ${player.displayName}`}
              >
                Remove
              </button>
            )}
          </li>
        ))}
      </ul>
      {lobby.players.length === 0 && (
        <p className="party-empty">
          No players are here yet. Share the code to invite friends.
        </p>
      )}
    </>
  )
}
