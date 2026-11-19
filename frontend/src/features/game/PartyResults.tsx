import { useNavigate } from 'react-router'
import { lobbiesApi, type Lobby } from '../../services/api/lobbies.ts'
import { useLobbyActions } from '../lobby/useLobbyActions.ts'

export function PartyResults({
  lobby,
  playerId,
  accept,
}: {
  lobby: Lobby
  playerId: string
  accept: (next: Lobby) => void
}) {
  const navigate = useNavigate()
  const { act, error, busy } = useLobbyActions(lobby.id, accept)
  return (
    <>
      <h1>Match results</h1>
      {lobby.lastResult ? (
        <>
          <h2>
            {lobby.lastResult.isTie ? 'Shared victory' : 'Winner'}:{' '}
            {lobby.lastResult.standings
              .filter((standing) => standing.rank === 1)
              .map((standing) => standing.displayName)
              .join(', ')}
          </h2>
          <ol aria-label="Final standings">
            {lobby.lastResult.standings.map((standing) => (
              <li key={standing.playerId}>
                {standing.displayName}: {standing.score} points (rank{' '}
                {standing.rank})
              </li>
            ))}
          </ol>
        </>
      ) : (
        <p>This match ended without a final score.</p>
      )}
      {error && <p role="alert">{error}</p>}
      <div className="lobby-actions">
        {playerId === lobby.hostPlayerId && (
          <button
            disabled={busy}
            onClick={() =>
              void act(() => lobbiesApi.rematch(lobby.id, lobby.version))
            }
          >
            Play Again
          </button>
        )}
        <button
          disabled={busy}
          onClick={() =>
            void act(
              () => lobbiesApi.leave(lobby.id),
              () => {
                void navigate('/')
              },
            )
          }
        >
          Leave lobby
        </button>
      </div>
      <p>
        Play Again keeps this lobby and its settings. Everyone must ready up
        again. Players who decline can leave.
      </p>
      {playerId !== lobby.hostPlayerId && (
        <p>Waiting for the host to choose Play Again.</p>
      )}
    </>
  )
}
