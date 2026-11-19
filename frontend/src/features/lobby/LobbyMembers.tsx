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
    <ul className="lobby-players" aria-label="Lobby players">
      {lobby.players.map((player) => (
        <li key={player.id}>
          <span>
            {player.displayName}
            {player.id === lobby.hostPlayerId ? ' (host)' : ''}
          </span>
          <span>
            {' '}
            — {player.isConnected ? 'Connected' : 'Reconnecting'};{' '}
            {player.isReady ? 'Ready' : 'Not ready'}
          </span>
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
  )
}
