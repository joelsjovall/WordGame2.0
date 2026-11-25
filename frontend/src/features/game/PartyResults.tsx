import { useNavigate } from 'react-router'
import { useEffect, useRef } from 'react'
import { gameAudio } from '../../services/gameAudio.ts'
import { lobbiesApi, type Lobby } from '../../services/api/lobbies.ts'
import { useLobbyActions } from '../lobby/useLobbyActions.ts'
import { MatchStandings } from './MatchStandings.tsx'
import { ShareMatchResult } from './ShareMatchResult.tsx'
import { Button, Card } from '../../components/ui.tsx'
import './game.css'

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
  const result = lobby.lastResult
  const heading = useRef<HTMLHeadingElement>(null)
  useEffect(() => {
    if (document.activeElement === document.body) heading.current?.focus()
  }, [])
  useEffect(() => {
    if (result?.winnerIds.includes(playerId))
      gameAudio.play(`${result.gameId}/victory`, 'victory')
  }, [result, playerId])
  return (
    <Card className="game-results">
      <h1 ref={heading} tabIndex={-1}>
        Match results
      </h1>
      {lobby.lastResult ? (
        <>
          <h2 aria-live="polite" aria-atomic="true">
            {lobby.lastResult.isTie ? 'Shared victory' : 'Winner'}:{' '}
            {lobby.lastResult.standings
              .filter((standing) => standing.rank === 1)
              .map((standing) => standing.displayName)
              .join(', ')}
          </h2>
          <MatchStandings standings={lobby.lastResult.standings} />
          <ShareMatchResult standings={lobby.lastResult.standings} />
        </>
      ) : (
        <p>This match ended without a final score.</p>
      )}
      {error && <p role="alert">{error}</p>}
      <div className="lobby-actions">
        {playerId === lobby.hostPlayerId && (
          <Button
            disabled={busy}
            onClick={() =>
              void act(() => lobbiesApi.rematch(lobby.id, lobby.version))
            }
          >
            Play Again
          </Button>
        )}
        <Button
          variant="secondary"
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
        </Button>
      </div>
      <p>
        Play Again keeps this lobby and its settings. Everyone must ready up
        again. Players who decline can leave.
      </p>
      {playerId !== lobby.hostPlayerId && (
        <p>Waiting for the host to choose Play Again.</p>
      )}
    </Card>
  )
}
