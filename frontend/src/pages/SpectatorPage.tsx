import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router'
import { Badge, Card, LoadingStatus } from '../components/ui.tsx'
import { apiErrorMessage } from '../services/api/client.ts'
import {
  spectatorsApi,
  type SpectatorState,
} from '../services/api/spectators.ts'
import { MatchStandings } from '../features/game/MatchStandings.tsx'
import { BluffHighlight } from '../features/game/BluffHighlight.tsx'
import { ServerCountdown } from '../features/game/ServerCountdown.tsx'
import '../features/game/game.css'

export function SpectatorPage() {
  const { lobbyId, lobbyCode } = useParams()
  const [state, setState] = useState<SpectatorState | null>(null)
  const [error, setError] = useState('')

  useEffect(() => {
    if (!lobbyId || !lobbyCode) return
    const controller = new AbortController()
    let timer: ReturnType<typeof setTimeout>
    async function refresh() {
      try {
        const next = await spectatorsApi.get(
          lobbyId!,
          lobbyCode!,
          controller.signal,
        )
        if (!controller.signal.aborted) {
          setState(next)
          setError('')
        }
      } catch (failure) {
        if (!controller.signal.aborted)
          setError(apiErrorMessage(failure, 'Unable to watch this match.'))
      } finally {
        if (!controller.signal.aborted)
          timer = setTimeout(() => void refresh(), 5000)
      }
    }
    void refresh()
    return () => {
      controller.abort()
      clearTimeout(timer)
    }
  }, [lobbyId, lobbyCode])

  const game = state?.game
  const round = game?.round
  return (
    <main className="game-page">
      <div className="game-shell">
        <h1>Watch Party match</h1>
        <p>You can follow the match here without joining as a player.</p>
        {!state && !error && (
          <LoadingStatus>Connecting to match...</LoadingStatus>
        )}
        {error && <p role="alert">{error}</p>}
        {state && !game && (
          <Card className="game-panel">
            <h2>Waiting for the match</h2>
            <p>
              The lobby is {state.lobbyStatus}. This page updates automatically.
            </p>
          </Card>
        )}
        {game && (
          <>
            <Card className="game-panel">
              <Badge>
                {game.status === 'completed' ? 'Final result' : 'Live match'}
              </Badge>
              <h2>
                Round {round?.number ?? 1} of {game.match.roundCount}
              </h2>
              <ol aria-label="Match scores">
                {game.players.map((player) => (
                  <li key={player.id}>
                    {player.displayName}: {player.score} points
                  </li>
                ))}
              </ol>
            </Card>
            {game.result ? (
              <Card className="game-panel">
                <h2>Match results</h2>
                <MatchStandings standings={game.result.standings} />
                <BluffHighlight highlight={game.result.bestBluffCall} />
              </Card>
            ) : (
              round && (
                <Card className="game-panel">
                  <h2>{round.category?.name ?? 'Choosing a category'}</h2>
                  <p>Phase: {round.status}</p>
                  {round.highestBid && (
                    <p>
                      Highest bid: {round.highestBid.wordCount} words
                      {round.highestBid.risk ? ' (risk bid)' : ''}
                    </p>
                  )}
                  {round.challenge && (
                    <>
                      <p>
                        {round.challenge.acceptedWordCount} of{' '}
                        {round.challenge.requiredWordCount} words accepted.
                      </p>
                      <ServerCountdown
                        deadline={round.challenge.deadline}
                        serverTime={game.serverTime}
                      />
                    </>
                  )}
                </Card>
              )
            )}
          </>
        )}
        <Link to="/">Back to homepage</Link>
      </div>
    </main>
  )
}
