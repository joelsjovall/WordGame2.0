import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router'
import { guestsApi } from '../services/api/guests.ts'
import { GameView } from '../features/game/GameView.tsx'
import '../features/lobby/lobby.css'
import '../features/matchmaking/matchmaking.css'

export function OnlineGamePage() {
  const { gameId = '' } = useParams()
  return <OnlineGame key={gameId} id={gameId} />
}
function OnlineGame({ id }: { id: string }) {
  const [playerId, setPlayerId] = useState('')
  const [error, setError] = useState('')
  useEffect(() => {
    let cancelled = false
    void guestsApi
      .ensure()
      .then((identity) => {
        if (!cancelled) setPlayerId(identity.playerId)
      })
      .catch(() => {
        if (!cancelled)
          setError(
            'Unable to restore your guest session. Return to Online Mode and try again.',
          )
      })
    return () => {
      cancelled = true
    }
  }, [])
  return (
    <main className="page online-page">
      <Link className="online-back" to="/">
        ← WordGame
      </Link>
      {error && <p role="alert">{error}</p>}
      {playerId ? (
        <GameView id={id} playerId={playerId} />
      ) : (
        !error && <p role="status">Restoring your match…</p>
      )}
    </main>
  )
}
