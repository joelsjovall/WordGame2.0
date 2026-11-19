import { useRef, useState } from 'react'
import { useNavigate } from 'react-router'
import { lobbiesApi, type Lobby } from '../../services/api/lobbies.ts'
import { ApiError } from '../../services/api/client.ts'

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
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const pending = useRef(false)
  async function act(rematch: boolean) {
    if (pending.current) return
    pending.current = true
    setBusy(true)
    setError('')
    try {
      const next = rematch
        ? await lobbiesApi.rematch(lobby.id, lobby.version)
        : await lobbiesApi.leave(lobby.id)
      if (rematch) accept(next)
      else void navigate('/')
    } catch (failure) {
      setError(
        failure instanceof ApiError
          ? failure.message
          : 'Unable to update the lobby. Please try again.',
      )
      try {
        accept(await lobbiesApi.get(lobby.id))
      } catch {
        /* Preserve the action error. */
      }
    } finally {
      pending.current = false
      setBusy(false)
    }
  }
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
          <button disabled={busy} onClick={() => void act(true)}>
            Play Again
          </button>
        )}
        <button disabled={busy} onClick={() => void act(false)}>
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
