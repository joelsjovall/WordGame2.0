import type { MatchmakingState } from '../../services/api/matchmaking.ts'
import { SearchTimer } from './SearchTimer.tsx'

export function MatchFound({
  state,
  opponent,
  disabled,
  respond,
}: {
  state: MatchmakingState
  opponent: string
  disabled: boolean
  respond: (action: 'accept' | 'decline') => void
}) {
  return (
    <>
      <p className="online-tag">Your next rival</p>
      <h2 aria-live="polite">Match found!</h2>
      <div className="online-opponent">
        <span className="online-avatar" aria-hidden="true">
          {Array.from(opponent)[0]?.toUpperCase() ?? '?'}
        </span>
        <strong>{opponent || 'Getting your opponent’s name…'}</strong>
        <span>Online · 1 vs 1</span>
      </div>
      {state.expiresAt && (
        <p className="online-clock">
          Time to accept: <SearchTimer timestamp={state.expiresAt} countdown />
        </p>
      )}
      <p>
        {state.hasAccepted
          ? 'You accepted. Waiting for your opponent…'
          : 'Ready to put your words to the test?'}
      </p>
      <div className="online-actions">
        <button
          className="online-primary"
          disabled={disabled || state.hasAccepted}
          onClick={() => respond('accept')}
        >
          {state.hasAccepted ? 'Accepted' : 'Accept match'}
        </button>
        <button disabled={disabled} onClick={() => respond('decline')}>
          Decline match
        </button>
      </div>
    </>
  )
}
