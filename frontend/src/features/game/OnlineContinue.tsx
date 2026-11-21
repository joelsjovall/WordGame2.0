import { useOnlineContinue } from './useOnlineContinue.ts'
import type { GameState } from '../../services/api/games.ts'

export function OnlineContinue({
  state,
  playerId,
}: {
  state: GameState
  playerId: string
}) {
  const { rematch, error, busy, act, retry } = useOnlineContinue(
    state,
    playerId,
  )
  const accepted = rematch?.acceptedPlayerIds.includes(playerId)
  return (
    <section className="online-card" aria-label="Continue playing">
      {error && (
        <p role="alert">
          {error}{' '}
          <button disabled={busy} onClick={retry}>
            Retry
          </button>
        </p>
      )}
      {rematch?.status === 'idle' && (
        <button disabled={busy} onClick={() => void act('request')}>
          Request Rematch
        </button>
      )}
      {rematch?.status === 'pending' && (
        <>
          <p role="status">
            {accepted
              ? 'Waiting for your opponent to agree…'
              : 'Your opponent requested a rematch.'}{' '}
            Requests expire after 30 seconds.
          </p>
          {!accepted && (
            <button disabled={busy} onClick={() => void act('accept')}>
              Accept Rematch
            </button>
          )}
          <button disabled={busy} onClick={() => void act('decline')}>
            Decline Rematch
          </button>
        </>
      )}
      {rematch?.status === 'declined' && (
        <p role="status">The rematch was declined.</p>
      )}
      {rematch?.status === 'expired' && (
        <p role="status">The rematch request expired.</p>
      )}
      {rematch?.status === 'unavailable' && (
        <p role="status">
          Your opponent is no longer available for this rematch.
        </p>
      )}
      {rematch?.status === 'ready' && (
        <p role="status">Starting your rematch…</p>
      )}
      <button
        disabled={busy || rematch?.status === 'ready'}
        onClick={() => void act('find')}
      >
        Find New Opponent
      </button>
    </section>
  )
}
