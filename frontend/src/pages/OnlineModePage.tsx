import { useEffect } from 'react'
import { Link, useNavigate } from 'react-router'
import { useMatchmaking } from '../features/matchmaking/useMatchmaking.ts'
import { MatchmakingPanel } from '../features/matchmaking/MatchmakingPanel.tsx'
import '../features/matchmaking/matchmaking.css'

export function OnlineModePage() {
  const matchmaking = useMatchmaking()
  const navigate = useNavigate()
  const { state } = matchmaking
  useEffect(() => {
    if (state?.status === 'ready' && state.gameId)
      void navigate(`/online/games/${encodeURIComponent(state.gameId)}`, {
        replace: true,
      })
  }, [state?.status, state?.gameId, navigate])
  return (
    <main className="page online-page">
      <Link className="online-back" to="/">
        ← WordGame
      </Link>
      <header className="online-heading">
        <p className="eyebrow">Online Mode · 1 vs 1</p>
        <h1>
          A worthy rival.
          <br />A war of words.
        </h1>
        <p>Meet a new opponent. Bid boldly. Call their bluff.</p>
      </header>
      {(matchmaking.error || matchmaking.actionError) && (
        <p className="online-error" role="alert">
          {matchmaking.error || matchmaking.actionError}
        </p>
      )}
      <div className="online-layout">
        <MatchmakingPanel matchmaking={matchmaking} />
        <aside className="online-guide" aria-label="How Online Mode works">
          <p className="online-tag">The game plan</p>
          <h2>
            A little knowledge.
            <br />A lot of nerve.
          </h2>
          <ol>
            <li>
              <strong>Find your rival</strong>
              <span>
                We’ll pair you with another guest. No registration needed.
              </span>
            </li>
            <li>
              <strong>Accept the challenge</strong>
              <span>Both players accept before the first round begins.</span>
            </li>
            <li>
              <strong>Bid. Bluff. Prove it.</strong>
              <span>
                Choose categories, outbid your rival, and back it up with words.
              </span>
            </li>
          </ol>
          <Link to="/lobbies/new">
            Want to play with friends? Create a private lobby →
          </Link>
        </aside>
      </div>
    </main>
  )
}
