import { useRef, useState } from 'react'
import { useGameState } from './useGameState.ts'
import { GameControls, type GameCommand } from './GameControls.tsx'
import { gamesApi } from '../../services/api/games.ts'
import { ApiError } from '../../services/api/client.ts'
import { gameSynchronization } from '../../services/gameSynchronization.ts'
import { GameConnectionStatus } from './GameConnectionStatus.tsx'
import { ServerCountdown } from './ServerCountdown.tsx'
import { Link } from 'react-router'
import { OnlineOutcome } from './OnlineOutcome.tsx'
import { OnlineContinue } from './OnlineContinue.tsx'

export function GameView({ id, playerId }: { id: string; playerId: string }) {
  const { state, connection, error: syncError } = useGameState(id)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const pending = useRef(false)
  const hasLeft =
    state?.players.find((player) => player.id === playerId)?.hasLeft === true
  async function send(command: GameCommand) {
    if (!state || pending.current) return
    pending.current = true
    setBusy(true)
    setError('')
    try {
      await gamesApi.act(id, {
        ...command,
        actionId: crypto.randomUUID(),
        expectedVersion: state.version,
      })
      await gameSynchronization.refresh(id)
    } catch (failure) {
      setError(
        failure instanceof ApiError
          ? failure.message
          : 'Unable to submit the action. Please try again.',
      )
      await gameSynchronization.refresh(id)
    } finally {
      pending.current = false
      setBusy(false)
    }
  }
  return (
    <>
      <h1>Match started</h1>
      <GameConnectionStatus connection={connection} />
      {(error || syncError) && (
        <p role="alert">{error || syncError?.message}</p>
      )}
      {!state &&
        (connection === 'connecting' || connection === 'recovering') && (
          <p>Loading game…</p>
        )}
      {state && (
        <>
          <p>
            Round {state.round?.number ?? 0} of {state.match.roundCount}
          </p>
          {state.round?.category && (
            <p>Category: {state.round.category.name}</p>
          )}
          <ul aria-label="Match scores">
            {state.players.map((player) => (
              <li key={player.id}>
                {player.displayName}: {player.score} points —{' '}
                {player.hasLeft ? (
                  'Left match'
                ) : player.isConnected ? (
                  'Connected'
                ) : player.reconnectUntil ? (
                  <>
                    Reconnecting, grace ends in{' '}
                    <ServerCountdown
                      key={player.reconnectUntil}
                      deadline={player.reconnectUntil}
                      serverTime={state.serverTime}
                    />{' '}
                    seconds
                  </>
                ) : (
                  'Offline'
                )}
              </li>
            ))}
          </ul>
          {state.status === 'abandoned' && (
            <p>
              {state.match.format === 'online'
                ? 'The match ended without a winner.'
                : 'The match ended before completion. Returning to the lobby results…'}
            </p>
          )}
          {state.status === 'completed' &&
            (state.match.format === 'online' ? (
              <OnlineOutcome state={state} playerId={playerId} />
            ) : (
              <p>The match is complete. Preparing final results…</p>
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
            <GameControls
              key={`${state.round?.id ?? ''}/${state.round?.status ?? ''}/${state.round?.highestBid?.wordCount ?? ''}`}
              state={state}
              playerId={playerId}
              busy={busy || connection !== 'connected'}
              send={(command) => void send(command)}
            />
          )}
          {!hasLeft && state.status === 'active' && (
            <button
              className="lobby-start"
              disabled={busy || connection !== 'connected'}
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
            </button>
          )}
          {!hasLeft &&
            state.status === 'active' &&
            state.match.format === 'online' && (
              <p>
                Forfeiting ends this match and awards the win to your opponent.
              </p>
            )}
        </>
      )}
    </>
  )
}
