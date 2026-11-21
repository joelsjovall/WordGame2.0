import { Link } from 'react-router'
import { Button } from '../../components/ui.tsx'
import { useGameState } from './useGameState.ts'
import { useGameActions } from './useGameActions.ts'
import { GameControls } from './GameControls.tsx'
import { GameConnectionStatus } from './GameConnectionStatus.tsx'
import { GameHeader } from './GameHeader.tsx'
import { PlayerScoreboard } from './PlayerScoreboard.tsx'
import { OnlineOutcome } from './OnlineOutcome.tsx'
import { OnlineContinue } from './OnlineContinue.tsx'
import './game.css'
import { useGameAudio } from './useGameAudio.ts'

export function GameView({ id, playerId }: { id: string; playerId: string }) {
  const { state, connection, error: syncError } = useGameState(id)
  useGameAudio(state, playerId, connection === 'connected')
  const hasLeft =
    state?.players.find((player) => player.id === playerId)?.hasLeft === true
  const canAct =
    state?.status === 'active' && connection === 'connected' && !hasLeft
  const { error, busy, send } = useGameActions(id, state?.version, canAct)
  return (
    <div className="game-shell">
      {state ? <GameHeader state={state} /> : <h1>Match started</h1>}
      <div className="game-connection">
        <GameConnectionStatus connection={connection} />
      </div>
      {(error || syncError) && (
        <p className="game-error" role="alert">
          {error || syncError?.message}
        </p>
      )}
      {!state &&
        (connection === 'connecting' || connection === 'recovering') && (
          <p role="status">
            <span className="wg-spinner" aria-hidden="true" /> Loading game...
          </p>
        )}
      {state && (
        <>
          <PlayerScoreboard state={state} playerId={playerId} />
          {state.status === 'abandoned' && (
            <section className="game-panel wg-card">
              <h2>Match ended</h2>
              <p>
                {state.match.format === 'online'
                  ? 'The match ended without a winner.'
                  : 'The match ended before completion. Returning to the lobby results...'}
              </p>
            </section>
          )}
          {state.status === 'completed' &&
            (state.match.format === 'online' ? (
              <OnlineOutcome state={state} playerId={playerId} />
            ) : (
              <p>The match is complete. Preparing final results...</p>
            ))}
          {state.match.format === 'online' &&
            (state.status === 'completed' || state.status === 'abandoned') && (
              <OnlineContinue state={state} playerId={playerId} />
            )}
          {hasLeft ? (
            <p>
              You left this match. Reopening this page will not rejoin it.{' '}
              <Link to="/">Back to homepage</Link>
            </p>
          ) : (
            <div
              key={`${state.round?.id}/${state.round?.status}`}
              className="game-stage"
              data-phase={state.round?.status}
            >
              <GameControls
                key={`${state.round?.id ?? ''}/${state.round?.status ?? ''}/${state.round?.highestBid?.wordCount ?? ''}`}
                state={state}
                playerId={playerId}
                busy={busy || !canAct}
                send={(command) => void send(command)}
              />
            </div>
          )}
          {!hasLeft && state.status === 'active' && (
            <footer className="game-exit">
              <Button
                variant="danger"
                disabled={busy || !canAct}
                onClick={() =>
                  void send({
                    type:
                      state.match.format === 'online'
                        ? 'forfeitMatch'
                        : 'leaveMatch',
                  })
                }
              >
                {state.match.format === 'online'
                  ? 'Forfeit Match'
                  : 'Leave match'}
              </Button>
              {state.match.format === 'online' && (
                <p>
                  Forfeiting ends this match and awards the win to your
                  opponent.
                </p>
              )}
            </footer>
          )}
        </>
      )}
    </div>
  )
}
