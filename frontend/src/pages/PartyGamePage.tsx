import { useEffect } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { useLobby } from '../features/lobby/useLobby.ts'
import { GameView } from '../features/game/GameView.tsx'
import { PartyResults } from '../features/game/PartyResults.tsx'
import '../features/lobby/lobby.css'

export function PartyGamePage() {
  const { lobbyId = '' } = useParams()
  return <GameRoom key={lobbyId} id={lobbyId} />
}
function GameRoom({ id }: { id: string }) {
  const navigate = useNavigate()
  const { lobby, playerId, error, unavailable, accept } = useLobby(id)
  useEffect(() => {
    if (lobby?.status === 'waiting' && !unavailable)
      void navigate(`/lobbies/${id}`, { replace: true })
  }, [lobby?.status, id, navigate, unavailable])
  return (
    <main className="page game-page">
      {error && <p role="alert">{error}</p>}
      {!lobby || unavailable ? (
        <>
          <h1>Party match</h1>
          <p>{error || 'Loading lobby…'}</p>
          <Link to="/">Back to homepage</Link>
        </>
      ) : lobby.status === 'completed' ? (
        <PartyResults lobby={lobby} playerId={playerId} accept={accept} />
      ) : lobby.gameId ? (
        <GameView key={lobby.gameId} id={lobby.gameId} playerId={playerId} />
      ) : (
        <p>Waiting for the match…</p>
      )}
    </main>
  )
}
