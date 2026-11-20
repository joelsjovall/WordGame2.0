import { useState } from 'react'
import type { useMatchmaking } from './useMatchmaking.ts'
import { SearchTimer } from './SearchTimer.tsx'
import { MatchFound } from './MatchFound.tsx'

export function MatchmakingPanel({
  matchmaking,
}: {
  matchmaking: ReturnType<typeof useMatchmaking>
}) {
  const [name, setName] = useState('')
  const { state, connection, busy, opponent, join, cancel, respond } =
    matchmaking
  const disabled = busy || connection !== 'connected'
  if (!state)
    return (
      <section className="online-card" aria-busy={connection !== 'stopped'}>
        <h2>Getting you ready</h2>
        <p role="status">
          {connection === 'stopped'
            ? 'Return home to start a new guest session.'
            : 'Connecting to Online Mode…'}
        </p>
      </section>
    )
  return (
    <section
      className="online-card"
      aria-label="Online matchmaking"
      aria-busy={busy}
    >
      {state.status === 'matched' ? (
        <MatchFound
          state={state}
          opponent={opponent}
          disabled={disabled}
          respond={(action) => {
            void respond(action)
          }}
        />
      ) : state.status === 'searching' ? (
        <>
          <div className="online-search-animation" aria-hidden="true">
            <span>W</span>
          </div>
          <h2 aria-live="polite">Finding your next rival</h2>
          <p>
            Looking for another word lover. Your match starts when you both
            accept.
          </p>
          {state.createdAt && (
            <p className="online-clock">
              Search time: <SearchTimer timestamp={state.createdAt} />
            </p>
          )}
          <button
            disabled={disabled}
            onClick={() => {
              void cancel()
            }}
          >
            Cancel Search
          </button>
        </>
      ) : state.status === 'ready' ? (
        <>
          <h2>Let’s play</h2>
          <p role="status">Opening your match…</p>
        </>
      ) : (
        <>
          <p className="online-tag">No account. Just words.</p>
          <h2>Find your match</h2>
          {state.status === 'expired' && (
            <p role="status">
              Your search or match invitation expired. You can search again.
            </p>
          )}
          {state.status === 'cancelled' && (
            <p role="status">
              Matchmaking cancelled. Play again whenever you’re ready.
            </p>
          )}
          {state.status === 'failed' && (
            <p role="status">
              We couldn’t create that match. Please search again.
            </p>
          )}
          <form
            onSubmit={(event) => {
              event.preventDefault()
              void join(name.trim())
            }}
          >
            <label htmlFor="online-name">Your display name</label>
            <input
              id="online-name"
              autoComplete="nickname"
              value={name}
              onChange={(event) => setName(event.target.value)}
              maxLength={32}
              required
              disabled={busy}
            />
            <button
              className="online-primary"
              disabled={disabled || !name.trim()}
            >
              {busy ? 'Joining…' : 'Play Online'}
            </button>
          </form>
          <p className="online-note">
            One opponent. Six rounds. Make every word count.
          </p>
        </>
      )}
    </section>
  )
}
